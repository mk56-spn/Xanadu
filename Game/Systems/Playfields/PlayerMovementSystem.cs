using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using Position = Xanadu.Ecs.Position;


namespace Xanadu.Systems.Playfields;

/// <summary>
/// Resolves dash and momentum notes. Both are hit with a direction key, and the
/// key is the way: the notes carry only their strength. A rhombus turns the
/// key 45° clockwise. A press claims at most one movement note, and the key is
/// remembered (<see cref="MoveKey"/>) for the hold-to-move ground mode.
/// </summary>
public sealed class PlayerMovementSystem : QuerySystem<Player, Position, PlayfieldView, PhysicsBody, Playhead>
{
    public const float SingleDashDistance = 250f;
    public const float DoubleDashDistance = 375f;
    public const float TripleDashDistance = 500f;
    private const float ActiveWindowMs = 150f;

    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly OffsetSettings _offsets;
    private readonly HashSet<Key> _previous = new();

    public PlayerMovementSystem(
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
        var pressed = new List<KeybindAction>();
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            if (JustPressed(_keybinds.Get(action)) && KeybindSettings.IsDirection(action))
                pressed.Add(action);
        }

        if (pressed.Count == 0)
            return;

        CommandBuffer buffer = CommandBuffer;
        var facings = new List<(Entity Entity, bool Left)>();
        var keys = new List<(Entity Entity, KeybindAction Key)>();

        Query.ForEachEntity((ref player, ref position, ref view, ref body, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
                return;

            Vector2 screen = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(entity, _offsets);
            CharacterKind? character = entity.TryGetComponent(
                out PlayerCharacter playerCharacter)
                ? playerCharacter.Kind
                : null;
            foreach (KeybindAction action in pressed)
            {
                if (!TryFindMovementNote(
                        action,
                        screen,
                        view,
                        judgedTime,
                        character,
                        out Entity noteEntity,
                        out Note movement))
                {
                    continue;
                }

                DashDirection heading = NoteRules.Heading(movement, action);
                ShaderPlay.Mark(movement.Kind == NoteKind.Dash ? ShaderEvent.Dash : ShaderEvent.Momentum);
                ShaderPlay.LastMove = DashDirectionVector(heading);
                GroundStepSystem.Cancel(entity, buffer);
                // Before the note moves the player, so the copy stays where it hit.
                HitGhostSystem.Spawn(_entities, entity);
                Color spray = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(movement));
                if (movement.Kind == NoteKind.Dash)
                {
                    HitEffects.SpawnDash(_entities, noteEntity, movement, view, heading);
                    Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(noteEntity, movement.Position));
                    ApplyDash(entity, ref body, ref position, screen, noteScreen, movement, heading, view, playhead, judgedTime, buffer);
                    // The path is told by a streak of particles, not a line.
                    _entities.CreateEntity(ParticleBurst.Streak(screen, position.Value, player.Size.X * 0.6f, spray));
                    _entities.CreateEntity(new ParticleBurst(
                        position.Value, GameParticles.Dash, spray, player.Size, heading: position.Value - screen));
                }
                else
                {
                    body.Body.SetLinearVelocity(MomentumVelocity(movement, heading));
                    PlayerFriction.Release(entity);
                    DashHangSystem.End(entity, body, buffer);
                    Vector2 push = screen + DashDirectionVector(heading) * 60f;
                    _entities.CreateEntity(new DashTrail(screen, push), EffectLayer.From(noteEntity));
                    _entities.CreateEntity(new ParticleBurst(
                        screen, GameParticles.Dash, spray, player.Size, heading: push - screen));
                }

                float horizontal = DashDirectionVector(heading).X;
                if (horizontal != 0f)
                    facings.Add((entity, horizontal < 0f));
                keys.Add((entity, action));

                if (entity.HasComponent<ScoreState>())
                {
                    ref ScoreState score = ref entity.GetComponent<ScoreState>();
                    PlayScoring.Apply(ref score, movement.TimeMs - judgedTime);
                    score.JudgementPosition = view.PlayfieldToScreen(GroupOffset.Shown(noteEntity, movement.Position));
                    score.JudgementTimeMs = playhead.TimeMs;
                    _entities.CreateEntity(
                        new HitTrail(
                            score.JudgementPosition,
                            PlayfieldRenderer.ColorFor(movement),
                            KeybindSettings.KeyLabel(_keybinds.Get(action))),
                        EffectLayer.From(noteEntity));
                }

                buffer.DeleteEntity(noteEntity.Id);
                break;
            }
        });

        // Structural changes have to wait until the query loop is done.
        foreach ((Entity entity, bool left) in facings)
            entity.AddComponent(new CharacterFacing { Left = left });
        foreach ((Entity entity, KeybindAction key) in keys)
            entity.AddComponent(new MoveKey(key));
    }

    /// <summary>
    /// Moves the player by the dash and hangs it there. The dash is measured
    /// from the note, so chained dashes land on the chart rather than on
    /// wherever the last hit left the player; on the ground, the floor's or
    /// block's standing height replaces the note's, or a short dash from a low
    /// note would do next to nothing. Vertical speed is cleared and horizontal
    /// kept; what follows runs from the note's time, not the press, so timing
    /// error never carries into the next note.
    /// </summary>
    private static void ApplyDash(
        Entity player,
        ref PhysicsBody body,
        ref Position position,
        Vector2 playerScreen,
        Vector2 noteScreen,
        Note movement,
        DashDirection heading,
        PlayfieldView view,
        Playhead playhead,
        long judgedTime,
        CommandBuffer buffer)
    {
        Vector2 origin = DashOrigin(player, view, playerScreen, noteScreen);
        Vector2 target = DashTarget(view.ScreenToPlayfield(origin), heading, movement.DashStrength);

        Vector2 velocity = new(body.Body.LinearVelocity.X, 0f);
        float late = (judgedTime - movement.TimeMs) / 1000f;
        Vector2 shift = velocity * late;

        Vector2 targetScreen = view.PlayfieldToScreen(target) + Box2DPhysicsWorld.ToScreen(shift);
        body.Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(targetScreen),
            body.Body.GetAngle());
        body.Body.SetLinearVelocity(velocity);
        position.Value = targetScreen;

        DashHangSystem.Begin(player, body, buffer, playhead, playhead.TimeMs - (judgedTime - movement.TimeMs), movement.TimeMs);
    }

    /// <summary>
    /// Where a dash is measured from, in screen space: the note, except that a
    /// player standing on the floor or a block dashes from its standing height,
    /// and the height is held to where the player's centre can be.
    /// </summary>
    public static Vector2 DashOrigin(Entity player, PlayfieldView view, Vector2 playerScreen, Vector2 noteScreen)
    {
        if (!player.TryGetComponent(out Playfield playfield))
            return noteScreen;

        float y = PlayerGround.TryStanding(player, view, playfield, playerScreen, out float standing)
            ? standing
            : noteScreen.Y;
        float half = player.GetComponent<Player>().Size.Y / 2;
        float top = view.ScreenOrigin.Y + half;
        float floor = view.ScreenOrigin.Y + playfield.Height * view.Scale - half;
        return new Vector2(noteScreen.X, Math.Clamp(y, top, floor));
    }

    /// <summary>
    /// The velocity a momentum note imposes: its speed along the heading, on
    /// both axes. It replaces whatever the body had, so notes never stack.
    /// </summary>
    public static Vector2 MomentumVelocity(Note note, DashDirection heading)
    {
        Vector2 direction = DashDirectionVector(heading) * MomentumSpeed(note);
        return new Vector2(direction.X, direction.Y);
    }

    /// <summary>The speed, in metres per second, a momentum note gives.</summary>
    public static float MomentumSpeed(Note note) =>
        note.Momentum / Box2DPhysicsWorld.PixelsPerMeter * Box2DPhysicsWorld.MomentumScale;

    private bool TryFindMovementNote(
        KeybindAction action,
        Vector2 playerScreen,
        PlayfieldView view,
        long judgedTime,
        CharacterKind? character,
        out Entity resultEntity,
        out Note result)
    {
        resultEntity = default;
        result = default;
        long bestError = long.MaxValue;
        float bestDistance = float.MaxValue;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsMovement(note.Kind) ||
                !NoteRules.Matches(note, action) ||
                entity.Tags.Has<NoteMissed>() ||
                Math.Abs(note.TimeMs - judgedTime) > ActiveWindowMs ||
                NoteFade.Alpha(note, judgedTime) <= 0)
            {
                continue;
            }

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position));
            if (!NoteReach.Contains(playerScreen, view, noteScreen, character))
                continue;

            long error = Math.Abs(note.TimeMs - judgedTime);
            float distance = playerScreen.DistanceSquaredTo(noteScreen);
            if (error > bestError ||
                (error == bestError && distance >= bestDistance))
            {
                continue;
            }

            bestError = error;
            bestDistance = distance;
            resultEntity = entity;
            result = note;
        }

        return !resultEntity.IsNull;
    }

    /// <summary>Where a dash from <paramref name="origin"/> lands, in playfield space.</summary>
    public static Vector2 DashTarget(
        Vector2 origin,
        DashDirection direction,
        DashStrength strength = DashStrength.Single) =>
        origin +
        DashDirectionVector(direction) * DashDistance(strength);

    /// <summary>How far a dash of this strength travels, in playfield units.</summary>
    public static float DashDistance(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => DoubleDashDistance,
            DashStrength.Triple => TripleDashDistance,
            _ => SingleDashDistance,
        };

    public static Vector2 DashDirectionVector(DashDirection direction) =>
        direction switch
        {
            DashDirection.Up => new Vector2(0, -1),
            DashDirection.UpRight => new Vector2(1, -1).Normalized(),
            DashDirection.Right => new Vector2(1, 0),
            DashDirection.DownRight => new Vector2(1, 1).Normalized(),
            DashDirection.Down => new Vector2(0, 1),
            DashDirection.DownLeft => new Vector2(-1, 1).Normalized(),
            DashDirection.Left => new Vector2(-1, 0),
            _ => new Vector2(-1, -1).Normalized(),
        };

    private bool IsWaiting(Entity entity)
    {
        if (!entity.HasComponent<PlaySession>())
            return false;

        PlaySession session = entity.GetComponent<PlaySession>();
        return !session.Started || session.Finished || session.Died;
    }

    private bool JustPressed(Key key)
    {
        bool now = _keyboard.IsPressed(key);
        bool was = _previous.Contains(key);

        if (now)
            _previous.Add(key);
        else
            _previous.Remove(key);

        return now && !was;
    }
}
