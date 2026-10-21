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
        bool left = JustPressed(_keybinds.Get(KeybindAction.L3));
        bool right = JustPressed(_keybinds.Get(KeybindAction.R3));
        bool up = JustPressed(_keybinds.Get(KeybindAction.M)) |
                  JustPressed(Key.Space);

        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref player, ref position, ref view, ref body, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
                return;

            if (!left && !right && !up)
                return;

            Vector2 screen = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());

            if (PlayerTouchesTrueNote(screen, player, view, playhead.TimeMs))
                return;

            Entity? active = NextActiveEvent(playhead.TimeMs);
            if (active is null)
                return;

            Vector2 dash = left
                ? new Vector2(-DashPixels, 0)
                : right
                    ? new Vector2(DashPixels, 0)
                    : new Vector2(0, -DashPixels);

            screen += dash;
            body.Body.SetTransform(
                Box2DPhysicsWorld.ToPhysics(screen),
                body.Body.GetAngle());
            position.Value = screen;

            buffer.AddTag<MovementEventUsed>(active.Value.Id);
        });
    }

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
            if (NoteFade.Alpha(note, timeMs) <= 0)
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

    private Entity? NextActiveEvent(long timeMs)
    {
        Entity? active = null;
        float closest = float.MaxValue;

        foreach (Entity entity in _entities.Query<MovementEvent>().ToEntityList())
        {
            if (entity.Tags.Has<MovementEventUsed>())
                continue;

            float distance = Math.Abs(
                entity.GetComponent<MovementEvent>().TimeMs - timeMs);

            if (distance > ActiveWindowMs || distance >= closest)
                continue;

            active = entity;
            closest = distance;
        }

        return active;
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
