using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Dashes the player while a movement queue is on them. The player is a Box2D
/// character, so gravity keeps acting between dashes.
/// </summary>
public sealed class PlayerMovementSystem : QuerySystem<Player, Position, PlayfieldView, PhysicsBody, Playhead>
{
    public const float DashPixels = 150f;
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
        var pressed = new HashSet<KeybindAction>();
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            if (JustPressed(_keybinds.Get(action)))
                pressed.Add(action);
        }

        if (pressed.Count == 0)
            return;

        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref player, ref position, ref view, ref body, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
                return;

            if (entity.HasComponent<PlayerCharacter>() &&
                !entity.Tags.Has<DashMovement>())
            {
                return;
            }

            Vector2 screen = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());

            if (PlayerTouchesTrueNote(screen, view, playhead.TimeMs))
                return;

            if (!TryFindMovementNote(
                    screen,
                    view,
                    playhead.TimeMs,
                    pressed,
                    out Note movement))
            {
                return;
            }

            Vector2 target = DashTarget(
                movement.Position,
                movement.DashDirection,
                movement.DashStrength);
            Vector2 targetScreen = view.PlayfieldToScreen(target);
            body.Body.SetTransform(
                Box2DPhysicsWorld.ToPhysics(targetScreen),
                body.Body.GetAngle());
            position.Value = targetScreen;
            float horizontal = DashDirectionVector(movement.DashDirection).X;
            if (horizontal != 0f)
                entity.AddComponent(new CharacterFacing { Left = horizontal < 0f });
            _entities.CreateEntity(new DashTrail(screen, targetScreen));
            _entities.CreateEntity(new ParticleBurst(targetScreen, ParticleKind.Dash));

            if (entity.HasComponent<ScoreState>())
            {
                ref ScoreState score = ref entity.GetComponent<ScoreState>();
                long judgedTime = playhead.TimeMs - _offsets.OffsetMs;
                PlayScoring.Apply(ref score, movement.TimeMs - judgedTime);
                score.JudgementPosition = view.PlayfieldToScreen(
                    movement.Position);
                score.JudgementTimeMs = playhead.TimeMs;
                _entities.CreateEntity(new HitTrail(
                    score.JudgementPosition,
                    PlayfieldRenderer.ColorFor(movement.Target),
                    KeybindSettings.KeyLabel(_keybinds.Get(movement.Target))));
            }

            foreach (Entity noteEntity in _entities
                         .Query<Note>()
                         .ToEntityList())
            {
                if (noteEntity.GetComponent<Note>().TimeMs == movement.TimeMs &&
                    noteEntity.GetComponent<Note>().Position == movement.Position &&
                    noteEntity.GetComponent<Note>().Kind == NoteKind.Movement)
                {
                    buffer.DeleteEntity(noteEntity.Id);
                    break;
                }
            }
        });
    }

    private bool TryFindMovementNote(
        Vector2 playerScreen,
        PlayfieldView view,
        long timeMs,
        HashSet<KeybindAction> pressed,
        out Note result)
    {
        result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Movement ||
                entity.Tags.Has<NoteMissed>() ||
                !pressed.Contains(note.Target) ||
                Math.Abs(note.TimeMs - timeMs) > ActiveWindowMs ||
                NoteFade.Alpha(note, timeMs) <= 0)
            {
                continue;
            }

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            if (!NoteReach.Contains(playerScreen, view, noteScreen))
                continue;

            result = note;
            return true;
        }

        return false;
    }

    public static Vector2 DashTarget(
        Vector2 notePosition,
        DashDirection direction,
        DashStrength strength = DashStrength.Single) =>
        notePosition +
        DashDirectionVector(direction) * DashPixels * DashStrengthMultiplier(strength);

    /// <summary>One, two or three dash lengths.</summary>
    public static int DashStrengthMultiplier(DashStrength strength) =>
        strength switch
        {
            DashStrength.Double => 2,
            DashStrength.Triple => 3,
            _ => 1,
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

    /// <summary>Whether a note the player can hit by key is within reach, so a press goes to it instead of a dash.</summary>
    private bool PlayerTouchesTrueNote(
        Vector2 playerScreen,
        PlayfieldView view,
        long timeMs)
    {
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Hit &&
                note.Kind != NoteKind.Contact &&
                note.Kind != NoteKind.CharacterSwitch)
            {
                continue;
            }

            if (NoteFade.Alpha(note, timeMs) <= 0)
                continue;

            if (NoteReach.Contains(
                    playerScreen,
                    view,
                    view.PlayfieldToScreen(note.Position)))
            {
                return true;
            }
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
