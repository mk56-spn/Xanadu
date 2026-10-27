using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;

namespace Xanadu.Systems.Groups;

/// <summary>Draws the F3 pressed-entity overlay and ECS/system metrics.</summary>
public sealed class DebugSystemGroup : SystemGroup
{
    public DebugSystemGroup(
        IRectCanvas canvas,
        Rid overlay,
        Rid parentCanvas,
        IOverlayToggleSource toggle,
        EntityStore store,
        SystemRoot systems,
        IFpsSource fps,
        EntityStore entitiesForSlider,
        IPointerInputSource pointer,
        Xanadu.Systems.Physics.Box2DPhysicsWorld physics,
        IOverlayToggleSource gravityToggle,
        IOverlayPageSource? paging = null) : base("Debug")
    {
        Add(new DebugPressedOverlaySystem(
            canvas,
            overlay,
            toggle,
            store,
            systems,
            fps,
            paging));
        Xanadu.Systems.Ui.UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas);
        Add(new GravityDebugSystem(
            entitiesForSlider,
            physics,
            gravityToggle));
    }
}
