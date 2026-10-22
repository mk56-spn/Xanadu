using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;
using Writer.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>
/// Dashes the player while a movement queue is on them. The player is a Box2D
/// character, so gravity keeps acting between dashes.
/// </summary>
public sealed class PlayerMovementSystem : QuerySystem<Player, Position, PlayfieldView, PhysicsBody, Playhead>
{
    private const float DashPixels = 150f;
    private const float ActiveWindowMs = 150f;
    private const float NoteSize = 24f;

    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly HashSet<Key> _previous = new();

    public PlayerMovementSystem(
        EntityStore entities,
        KeybindSettings keybinds,
        IKeyboardState keyboard)
    {
        _entities = entities;
        _keybinds = keybinds;
        _keyboard = keyboard;
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

            Vector2 screen = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());

            if (PlayerTouchesTrueNote(screen, player, view, playhead.TimeMs))
                return;

            if (!TryFindMovementNote(
                    screen,
                    player,
                    view,
                    playhead.TimeMs,
                    pressed,
                    out Note movement))
            {
                return;
            }

            Vector2 direction = DashDirectionVector(movement.DashDirection);
            Vector2 target = movement.Position + direction * DashPixels;
            Vector2 targetScreen = view.PlayfieldToScreen(target);
            body.Body.SetTransform(
                Box2DPhysicsWorld.ToPhysics(targetScreen),
                body.Body.GetAngle());
            position.Value = targetScreen;

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
        Player player,
        PlayfieldView view,
        long timeMs,
        HashSet<KeybindAction> pressed,
        out Note result)
    {
        result = default;
        float noteSize = PlayfieldRenderer.NoteSize * view.Scale;
        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Movement ||
                !pressed.Contains(note.Target) ||
                Math.Abs(note.TimeMs - timeMs) > ActiveWindowMs ||
                NoteFade.Alpha(note, timeMs) <= 0)
            {
                continue;
            }

            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            var noteRect = new Rect2(
                noteScreen - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));
            if (!playerRect.Intersects(noteRect))
                continue;

            result = note;
            return true;
        }

        return false;
    }

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

    private bool PlayerTouchesTrueNote(
        Vector2 playerScreen,
        Player player,
        PlayfieldView view,
        long timeMs)
    {
        var playerRect = new Rect2(
            playerScreen - player.Size / 2,
            player.Size);

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (entity.Tags.Has<NoteMissed>())
                continue;

            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Hit ||
                NoteFade.Alpha(note, timeMs) <= 0)
                continue;

            float noteSize = NoteSize * view.Scale;
            Vector2 noteScreen = view.PlayfieldToScreen(note.Position);
            var noteRect = new Rect2(
                noteScreen - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));

            if (playerRect.Intersects(noteRect))
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
