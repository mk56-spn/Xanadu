using System;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Wiring.Scenes;

/// <summary>Notifies Godot-facing code when the ECS scene system changes scenes.</summary>
public sealed class SceneDirector
{
    public SceneKind Current { get; private set; } = SceneKind.None;

    public event Action<SceneKind>? Changed;

    internal void Apply(SceneKind scene)
    {
        Current = scene;
        Changed?.Invoke(scene);
    }
}
