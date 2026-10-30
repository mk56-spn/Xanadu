using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Hits notes when the player is within <see cref="NoteReach"/> of them and the
/// note's target key is pressed inside the timing window. Mines keep using the
/// character's damage box.
/// </summary>
public sealed partial class PlayNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly OffsetSettings _offsets;
    private readonly HashSet<Key> _held = new();

    public PlayNoteInputSystem(
        EntityStore entities,
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings? offsets = null)
    {
        _entities = entities;
        _keybinds = keybinds;
        _keyboard = keyboard;
        _offsets = offsets ?? new OffsetSettings(persist: false);
    }

    protected override void OnUpdate()
    {
        Entity playfield = FindPlayfield();
        if (playfield.IsNull || playfield.Tags.Has<PlayPaused>())
            return;

        if (playfield.HasComponent<PlaySession>())
        {
            PlaySession session = playfield.GetComponent<PlaySession>();
            if (!session.Started || session.Finished)
                return;
        }

        SplitlineState splitline = playfield.TryGetComponent(out SplitlineState state)
            ? state
            : default;

        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Key key = _keybinds.Get(action);
            bool pressed = _keyboard.IsPressed(key);
            bool wasPressed = _held.Contains(key);

            if (pressed)
                _held.Add(key);
            else
                _held.Remove(key);

            if (!pressed || wasPressed)
                continue;

            bool absorbed = splitline.LeftActive && NoteRules.IsLeft(action) ||
                            splitline.RightActive && NoteRules.IsRight(action);
            if (absorbed)
            {
                TryRailHit(action, playfield);
                continue;
            }

            if (!TryMine(action, playfield))
                TryHit(action, playfield);
        }

        MarkMisses(playfield);
    }

    private bool TryRailHit(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        Entity hit = default;
        Note hitNote = default;
        long bestError = long.MaxValue;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsRail(note.Kind) ||
                note.ChannelSide != NoteRules.SideOf(action) ||
                note.PairIndex != NoteRules.PairIndexFor(action))
            {
                continue;
            }

            long error = Math.Abs(note.TimeMs - judgedTime);
            if (error > window || NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            if (error >= bestError)
                continue;

            bestError = error;
            hit = entity;
            hitNote = note;
        }

        if (hit.IsNull)
            return false;

        Vector2 position = SplitlineRail.ScreenPosition(
            view,
            playfield.GetComponent<Playfield>(),
            hitNote,
            playhead.TimeMs);
        float radius = SplitlineRail.NoteRadius * view.Scale;
        Color color = GroupColor.For(hit, PlayfieldRenderer.ColorFor(hitNote));

        score.JudgementPosition = position;
        score.JudgementTimeMs = playhead.TimeMs;
        PlayScoring.Apply(ref score, hitNote.TimeMs - judgedTime);

        _entities.CreateEntity(new HitTrail(
            position,
            PlayfieldRenderer.ColorFor(hitNote),
            KeybindSettings.KeyLabel(_keybinds.Get(action))));
        _entities.CreateEntity(new NotePop(
            hitNote,
            position,
            color,
            radius,
            PlayfieldRenderer.NotePopLifetime));

        if (hitNote.HoldMs > 0)
        {
            _entities.CreateEntity(new RailHold(
                action,
                hitNote.ChannelSide,
                hitNote.PairIndex,
                hitNote.TimeMs,
                hitNote.TimeMs + hitNote.HoldMs,
                playfield));
        }

        hit.DeleteEntity();
        return true;
    }

    private bool TryMine(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Player player = playfield.GetComponent<Player>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;
        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;

        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);
        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (Math.Abs(mine.TimeMs - judgedTime) > window)
                continue;

            if (MineFade.Alpha(mine, playhead.TimeMs) <= 0)
                continue;

            Vector2 mineScreen = view.PlayfieldToScreen(mine.Position);
            var mineRect = new Rect2(
                mineScreen - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (!playerRect.Intersects(mineRect))
                continue;

            score.AccuracyPenalty +=
                ScoreRules.MineAccuracyPenalty * score.TotalNotes;
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Mine;
            score.JudgementPosition = mineScreen;
            score.JudgementTimeMs = playhead.TimeMs;

            _entities.CreateEntity(new HitTrail(
                mineScreen,
                Palette.DangerBright,
                KeybindSettings.KeyLabel(_keybinds.Get(action))));
            entity.DeleteEntity();
            return true;
        }

        return false;
    }

    private void TryHit(KeybindAction action, Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        ref PlayfieldView view = ref playfield.GetComponent<PlayfieldView>();
        Vector2 playerScreen = playfield.GetComponent<Position>().Value;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;
        long missWindow = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        CharacterKind? character = playfield.TryGetComponent(
            out PlayerCharacter playerCharacter)
            ? playerCharacter.Kind
            : null;
        bool armed = character == CharacterKind.Armed;
        ArmedWeapon weapon = playfield.TryGetComponent(out ArmedWeapon equipped)
            ? equipped
            : new ArmedWeapon(WeaponKind.Spear);
        bool facesLeft = playfield.TryGetComponent(out CharacterFacing facing) &&
                         facing.Left;

        Entity hit = default;
        Note hitNote = default;
        long bestError = long.MaxValue;
        long bestOffset = 0;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsKeyed(note.Kind) ||
                !NoteRules.ActiveFor(note.Kind, character) ||
                !NoteRules.Matches(note, action) ||
                !CanFormStep(playfield, note))
            {
                continue;
            }

            long offset = note.TimeMs - judgedTime;
            long error = Math.Abs(offset);
            if (error > missWindow)
                continue;

            if (NoteFade.Alpha(note, judgedTime) <= 0)
                continue;

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            bool inRange = armed && (note.Kind is NoteKind.Hit or NoteKind.Pair)
                ? WeaponHitRules.Contains(
                    weapon.Kind,
                    playerScreen,
                    facesLeft,
                    noteScreen,
                    view.Scale)
                : NoteReach.Contains(playerScreen, view, noteScreen);
            if (!inRange)
                continue;

            if (error >= bestError)
                continue;

            bestError = error;
            bestOffset = offset;
            hit = entity;
            hitNote = note;
        }

        if (hit.IsNull)
            return;

        if (armed && (hitNote.Kind is NoteKind.Hit or NoteKind.Pair))
        {
            HitSwing(
                action,
                hit,
                hitNote,
                playfield,
                ref score,
                view,
                playerScreen,
                judgedTime,
                playhead.TimeMs,
                weapon.Kind,
                facesLeft);
            return;
        }

        HitNote(
            action,
            hit,
            hitNote,
            bestOffset,
            playfield,
            ref score,
            view,
            playerScreen,
            playhead.TimeMs,
            triggerAttack: true);
    }

    private bool Hit(
        ref ScoreState score,
        Entity noteEntity,
        long offsetMs,
        Entity playfield,
        KeybindAction action)
    {
        Note note = noteEntity.GetComponent<Note>();

        if (note.Kind == NoteKind.CharacterSwitch)
            ApplyCharacter(playfield, note.Character);
        else if (note.Kind == NoteKind.WeaponSwitch)
            ApplyWeapon(playfield, action);
        else if (note.Kind == NoteKind.Channel)
            StartChannel(playfield, note);
        else if (note.Kind == NoteKind.Construction)
            StartConstruction(playfield, note);
        else if (note.Kind == NoteKind.ConstructionStep)
            BuildConstructionStep(playfield, note);

        PlayScoring.Apply(ref score, offsetMs);

        // Normal and pair hits feed the Contact bank on the side that was
        // pressed. Only Contact accumulates; the bank itself survives a
        // character switch.
        if ((note.Kind == NoteKind.Hit || note.Kind == NoteKind.Pair) &&
            playfield.HasComponent<ContactCounters>() &&
            BanksContact(playfield) &&
            NoteRules.HasPairIndex(action))
        {
            playfield.GetComponent<ContactCounters>().Add(action);
        }

        noteEntity.DeleteEntity();
        return true;
    }

    /// <summary>Whether the active character banks side hits for Contact barriers.</summary>
    private static bool BanksContact(Entity playfield) =>
        !playfield.TryGetComponent(out PlayerCharacter character) ||
        character.Kind == CharacterKind.Contact;

    private static void ApplyWeapon(Entity playfield, KeybindAction action)
    {
        WeaponKind kind = NoteRules.IsLeft(action)
            ? WeaponKind.Spear
            : WeaponKind.Axe;
        if (playfield.HasComponent<ArmedWeapon>())
            playfield.GetComponent<ArmedWeapon>().Kind = kind;
        else
            playfield.AddComponent(new ArmedWeapon(kind));
    }

    /// <summary>
    /// Absorbs one side until its authored end marker. The end marker is chart
    /// data; once consumed it leaves the playfield.
    /// </summary>
    private void StartChannel(Entity playfield, Note start)
    {
        long endMs = start.TimeMs + 4_000;
        Entity endMarker = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note candidate = entity.GetComponent<Note>();
            if (!NoteRules.IsChannelEnd(candidate.Kind) ||
                candidate.ChannelSide != start.ChannelSide ||
                candidate.TimeMs < start.TimeMs)
            {
                continue;
            }

            if (!endMarker.IsNull && candidate.TimeMs >= endMs)
                continue;

            endMs = candidate.TimeMs;
            endMarker = entity;
        }

        if (!endMarker.IsNull)
            endMarker.DeleteEntity();

        if (!playfield.HasComponent<SplitlineState>())
            playfield.AddComponent(new SplitlineState());

        ref SplitlineState state = ref playfield.GetComponent<SplitlineState>();
        if (start.ChannelSide == ChannelSide.Left)
        {
            state.LeftActive = true;
            state.LeftEndMs = endMs;
        }
        else
        {
            state.RightActive = true;
            state.RightEndMs = endMs;
        }
    }

    private static bool CanFormStep(Entity playfield, Note note)
    {
        if (note.Kind != NoteKind.ConstructionStep)
            return true;

        return playfield.TryGetComponent(out ConstructionState state) &&
               state.Active &&
               state.ShapeId == note.ShapeId;
    }

    private void StartConstruction(Entity playfield, Note note)
    {
        int total = 0;
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (entity.GetComponent<Block>().ShapeId == note.ShapeId)
                total++;
        }

        if (!playfield.HasComponent<ConstructionState>())
            playfield.AddComponent(new ConstructionState());

        ref ConstructionState state =
            ref playfield.GetComponent<ConstructionState>();
        state.ShapeId = note.ShapeId;
        state.BuiltPieces = 0;
        state.TotalPieces = total;
        state.Active = total > 0;
    }

    private void BuildConstructionStep(Entity playfield, Note note)
    {
        if (!playfield.TryGetComponent(out ConstructionState state) ||
            !state.Active ||
            state.ShapeId != note.ShapeId)
        {
            return;
        }

        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            if (block.ShapeId != note.ShapeId ||
                block.ShapePiece != note.ShapePiece)
            {
                continue;
            }

            entity.RemoveTag<ConstructionHidden>();
            _entities.CreateEntity(new ParticleBurst(
                view.PlayfieldToScreen(block.Center),
                ParticleKind.Dash));
            break;
        }

        ref ConstructionState current =
            ref playfield.GetComponent<ConstructionState>();
        current.BuiltPieces++;
        if (current.BuiltPieces >= current.TotalPieces)
            current.Active = false;
    }

    private static void ApplyCharacter(Entity playfield, CharacterKind kind)
    {
        if (!playfield.HasComponent<PlayerCharacter>())
            return;

        playfield.GetComponent<PlayerCharacter>().Kind = kind;

        // The contact bank is a run resource; switching away only stops it
        // growing. The special rules for the other characters arrive with
        // their kits.
        if (kind == CharacterKind.Contact &&
            !playfield.HasComponent<ContactCounters>())
        {
            playfield.AddComponent(new ContactCounters());
        }

        if (kind == CharacterKind.Armed &&
            !playfield.HasComponent<ArmedWeapon>())
        {
            playfield.AddComponent(new ArmedWeapon(WeaponKind.Spear));
        }
    }

    private void MarkMisses(Entity playfield)
    {
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        long judgedTime = playhead.TimeMs - _offsets.OffsetMs;

        if (playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
            return;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            // Contact barriers and channel-end markers are chart checks, not
            // scored notes.
            if (note.Kind is NoteKind.Contact or NoteKind.ChannelEnd)
                continue;

            long missWindow =
                (long)ScoreRules.JudgementDeviation(Judgement.Miss);
            if (note.TimeMs + missWindow >= judgedTime)
                continue;

            entity.AddTag<NoteMissed>();
            score.Combo = 0;
            score.HasJudgement = true;
            score.LastJudgement = Judgement.Miss;
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            score.JudgementPosition = NoteRules.IsRail(note.Kind)
                ? SplitlineRail.ScreenPosition(
                    view,
                    playfield.GetComponent<Playfield>(),
                    note,
                    playhead.TimeMs)
                : view.PlayfieldToScreen(note.Position);
            score.JudgementTimeMs = playhead.TimeMs;
            PlayScoring.Count(ref score, Judgement.Miss);
        }
    }

    private Entity FindPlayfield()
    {
        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldView, PlayfieldMode, ScoreState>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Play &&
                entity.HasComponent<Player>() &&
                entity.HasComponent<Position>())
            {
                return entity;
            }
        }

        return default;
    }
}
