using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Writer.Ecs;

namespace Writer.Systems.Scenes;

public readonly record struct SceneActionContext(
    SceneSystem Scenes,
    EntityStore Entities,
    Entity Entity,
    SceneCommand Command);

/// <summary>Scene-scoped command handlers, registered by static scene action classes.</summary>
public sealed class SceneActionRegistry
{
    private readonly Dictionary<(SceneKind, SceneActionId),
        Action<SceneActionContext>> _handlers = new();

    public void Register(
        SceneKind scene,
        SceneActionId action,
        Action<SceneActionContext> handler)
    {
        _handlers[(scene, action)] = handler;
    }

    public bool TryInvoke(
        SceneKind scene,
        SceneActionContext context)
    {
        if (!_handlers.TryGetValue(
                (scene, context.Command.Action),
                out Action<SceneActionContext>? handler))
        {
            return false;
        }

        handler(context);
        return true;
    }
}
