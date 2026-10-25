using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Canvas;
using Writer.Systems.Groups;
using Writer.Systems.Input;

namespace Writer.Systems.Ui;

/// <summary>Complete generic UI boot for standalone UI hosts.</summary>
public sealed class UiSystemGroup : SystemGroup
{
    public UiSystemGroup(
        EntityStore entities,
        Rid parentCanvas,
        IRectCanvas canvas,
        IPointerInputSource pointer,
        IOverlayToggleSource? debugToggle = null) : base("Ui")
    {
        Scroll = new ScrollSystem();
        Add(new InitializationSystemGroup(parentCanvas));
        Add(new PresentationSystemGroup(canvas));
        Add(new UiInteractionSystemGroup(entities, pointer, Scroll));
        UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas,
            debugToggle);
    }

    public ScrollSystem Scroll { get; }
}
