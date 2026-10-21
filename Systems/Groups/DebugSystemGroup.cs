using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Input;

namespace Writer.Systems.Groups;

/// <summary>Draws the F3 pressed-entity overlay and ECS/system metrics.</summary>
public sealed class DebugSystemGroup : SystemGroup
{
    public DebugSystemGroup(
        IRectCanvas canvas,
        Rid overlay,
        IOverlayToggleSource toggle,
        EntityStore store,
        SystemRoot systems,
        IFpsSource fps) : base("Debug")
    {
        Add(new DebugPressedOverlaySystem(canvas, overlay, toggle, store, systems, fps));
    }
}
