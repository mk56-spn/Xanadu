using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;

namespace Writer.Systems.Playfields;

/// <summary>Lights active keys and records press/release segments over time.</summary>
public sealed class PlayKeyTrailSystem : QuerySystem<Playhead, KeyTrail>
{
    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly HashSet<KeybindAction> _held = new();
    private readonly Dictionary<KeybindAction, Entity> _active = new();

    public PlayKeyTrailSystem(
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
        float delta = Tick.deltaTime;

        Query.ForEachEntity((ref playhead, ref trail, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() ||
                (entity.HasComponent<PlaySession>() &&
                 !entity.GetComponent<PlaySession>().Started))
            {
                return;
            }

            foreach (KeybindAction action in KeybindSettings.Actions)
            {
                bool pressed = _keyboard.IsPressed(_keybinds.Get(action));
                bool wasPressed = _held.Contains(action);

                float value = trail.Get(action);
                value = pressed
                    ? 1f
                    : Math.Max(0f, value - delta * 3f);
                trail.Set(action, value);

                if (pressed == wasPressed)
                    continue;

                if (pressed)
                {
                    _held.Add(action);
                    Entity segment = _entities.CreateEntity(
                        new KeyPressTrail(action, playhead.TimeMs));
                    _active[action] = segment;
                }
                else
                {
                    _held.Remove(action);
                    if (_active.TryGetValue(action, out Entity segment) &&
                        !segment.IsNull &&
                        segment.HasComponent<KeyPressTrail>())
                    {
                        segment.GetComponent<KeyPressTrail>().EndMs =
                            playhead.TimeMs;
                    }

                    _active.Remove(action);
                }
            }
        });
    }
}
