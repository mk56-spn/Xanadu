using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Input;
using Writer.Systems.Ui;

namespace Writer.Systems.Groups;

/// <summary>Draws the F3 pressed-entity overlay and ECS/system metrics.</summary>
public sealed class DebugSystemGroup : SystemGroup
{
    public DebugSystemGroup(
        IRectCanvas canvas,
        Rid overlay,
        Rid elementsOverlay,
        IOverlayToggleSource toggle,
        IOverlayToggleSource elementsToggle,
        EntityStore store,
        SystemRoot systems,
        IFpsSource fps,
        EntityStore entitiesForSlider,
        IPointerInputSource pointer,
        Writer.Systems.Physics.Box2DPhysicsWorld physics,
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
        Add(new DebugElementOverlaySystem(canvas, elementsOverlay, elementsToggle));
        Add(new Writer.Systems.Ui.UiSliderSystem(pointer));
        Add(new GravityDebugSystem(
            entitiesForSlider,
            physics,
            gravityToggle));
    }
}
