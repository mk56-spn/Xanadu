using Friflo.Engine.ECS;
using Godot;

namespace Writer.Systems.Ui;

/// <summary>Tracks a pointer drag without imposing movement semantics.</summary>
public sealed class UiDragController(float threshold = 4f)
{
    public Entity Target { get; private set; }
    public Vector2 StartPointer { get; private set; }
    public Vector2 CurrentPointer { get; private set; }
    public bool Active { get; private set; }

    public Vector2 Delta => CurrentPointer - StartPointer;

    public bool Begin(Entity target, Vector2 pointer)
    {
        if (target.IsNull)
            return false;

        Target = target;
        StartPointer = pointer;
        CurrentPointer = pointer;
        Active = false;
        return true;
    }

    public bool Update(Vector2 pointer)
    {
        if (Target.IsNull)
            return false;

        CurrentPointer = pointer;
        if (!Active && StartPointer.DistanceTo(pointer) >= threshold)
            Active = true;

        return Active;
    }

    public void End()
    {
        Target = default;
        StartPointer = default;
        CurrentPointer = default;
        Active = false;
    }
}
