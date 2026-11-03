using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Resolves dash and momentum notes. Both are claimed by any key: a press
/// first goes to a normal or pair note it can hit, and only an unclaimed press
/// moves the player. A press never claims more than one movement note.
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
            if (JustPressed(_keybinds.Get(action)))
                pressed.Add(action);
        }

        if (pressed.Count == 0)
            return;

        CommandBuffer buffer = CommandBuffer;
        var facings = new List<(Entity Entity, bool Left)>();

        Query.ForEachEntity((ref player, ref position, ref view, ref body, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
                return;

            SplitlineState splitline = entity.TryGetComponent(out SplitlineState state)
                ? state
                : default;
            if (splitline.Absent)
                return;

            Vector2 screen = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(entity, _offsets);
            CharacterKind? character = entity.TryGetComponent(
                out PlayerCharacter playerCharacter)
                ? playerCharacter.Kind
                : null;
            bool armed = character == CharacterKind.Armed;
            WeaponKind weapon = entity.TryGetComponent(out ArmedWeapon equipped)
                ? equipped.Kind
                : WeaponKind.Spear;
            bool facesLeft = entity.TryGetComponent(out CharacterFacing facing) &&
                             facing.Left;

            foreach (KeybindAction action in pressed)
            {
                if (splitline.LeftActive && NoteRules.IsLeft(action) ||
                    splitline.RightActive && NoteRules.IsRight(action))
                {
                    continue;
                }

                // A press that can hit a note belongs to the note first.
                if (PressHitsNote(
                        action,
                        screen,
                        view,
                        judgedTime,
                        character,
                        armed,
                        weapon,
                        facesLeft))
                {
                    continue;
                }

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

                Color spray = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(movement));
                if (movement.Kind == NoteKind.Dash)
                {
                    HitEffects.SpawnDash(_entities, noteEntity, movement, view);
                    ApplyDash(ref body, ref position, screen, movement, view);
                    _entities.CreateEntity(new DashTrail(screen, position.Value));
                    _entities.CreateEntity(new ParticleBurst(position.Value, ParticleKind.Dash, spray, player.Size));
                }
                else
                {
                    ApplyMomentum(ref body, movement);
                    Vector2 push = screen + DashDirectionVector(movement.DashDirection) * 60f;
                    _entities.CreateEntity(new DashTrail(screen, push));
                    _entities.CreateEntity(new ParticleBurst(screen, ParticleKind.Dash, spray, player.Size));
                }

                float horizontal = DashDirectionVector(movement.DashDirection).X;
                if (horizontal != 0f)
                    facings.Add((entity, horizontal < 0f));

                if (entity.HasComponent<ScoreState>())
                {
                    ref ScoreState score = ref entity.GetComponent<ScoreState>();
                    PlayScoring.Apply(ref score, movement.TimeMs - judgedTime);
                    score.JudgementPosition = view.PlayfieldToScreen(movement.Position);
                    score.JudgementTimeMs = playhead.TimeMs;
                    _entities.CreateEntity(new HitTrail(
                        score.JudgementPosition,
                        PlayfieldRenderer.ColorFor(movement),
                        KeybindSettings.KeyLabel(_keybinds.Get(action))));
                }

                buffer.DeleteEntity(noteEntity.Id);
                break;
            }
        });

        // Structural changes have to wait until the query loop is done.
        foreach ((Entity entity, bool left) in facings)
            entity.AddComponent(new CharacterFacing { Left = left });
    }

    private static void ApplyDash(
        ref PhysicsBody body,
        ref Position position,
        Vector2 playerScreen,
        Note movement,
        PlayfieldView view)
    {
        Vector2 target = DashTarget(
            view.ScreenToPlayfield(playerScreen),
            movement.DashDirection,
            movement.DashStrength);
        Vector2 targetScreen = view.PlayfieldToScreen(target);
        body.Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(targetScreen),
            body.Body.GetAngle());
        body.Body.SetLinearVelocity(new NVector2(body.Body.LinearVelocity.X, 0f));
        position.Value = targetScreen;
    }

    private static void ApplyMomentum(
        ref PhysicsBody body,
        Note movement)
    {
        body.Body.SetLinearVelocity(WithoutOpposingSpeed(
            body.Body.LinearVelocity,
            DashDirectionVector(movement.DashDirection)));
        body.Body.ApplyLinearImpulseToCenter(
            MomentumImpulse(movement, body.Body.Mass),
            true);
    }

    /// <summary>
    /// Zeroes speed on each axis that runs against <paramref name="direction"/>, so a
    /// change of direction goes straight to the note's speed. Speed along it stacks,
    /// and speed across it is kept.
    /// </summary>
    public static NVector2 WithoutOpposingSpeed(NVector2 velocity, Vector2 direction) =>
        new(
            velocity.X * direction.X < 0f ? 0f : velocity.X,
            velocity.Y * direction.Y < 0f ? 0f : velocity.Y);

    /// <summary>The impulse a momentum note applies to a body of <paramref name="mass"/> kg.</summary>
    public static NVector2 MomentumImpulse(Note note, float mass)
    {
        Vector2 direction = DashDirectionVector(note.DashDirection);
        float velocityChange = note.Momentum / Box2DPhysicsWorld.PixelsPerMeter *
                               Box2DPhysicsWorld.MomentumScale;
        return new NVector2(
            direction.X * velocityChange * mass,
            direction.Y * velocityChange * mass);
    }

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

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
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
        return !session.Started || session.Finished;
    }

    /// <summary>
    /// Whether this key would hit a note right now, so the press must not be
    /// spent on a movement note. Pair notes match by index, so either side
    /// counts; normal notes match their own key.
    /// </summary>
    private bool PressHitsNote(
        KeybindAction action,
        Vector2 playerScreen,
        PlayfieldView view,
        long judgedTime,
        CharacterKind? character,
        bool armed,
        WeaponKind weapon,
        bool facesLeft)
    {
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (!NoteRules.IsKeyed(note.Kind) ||
                !NoteRules.ActiveFor(note.Kind, character) ||
                !NoteRules.Matches(note, action) ||
                Math.Abs(note.TimeMs - judgedTime) > window ||
                NoteFade.Alpha(note, judgedTime) <= 0)
            {
                continue;
            }

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            bool inRange = armed && (note.Kind is NoteKind.Hit or NoteKind.Pair)
                ? WeaponHitRules.Contains(
                    weapon,
                    playerScreen,
                    facesLeft,
                    noteScreen,
                    view.Scale)
                : NoteReach.Contains(playerScreen, view, noteScreen, character);
            if (inRange)
                return true;
        }

        return false;
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
