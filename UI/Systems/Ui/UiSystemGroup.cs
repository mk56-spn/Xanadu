using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Groups;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Ui;

/// <summary>Complete generic UI boot for standalone UI hosts.</summary>
public sealed class UiSystemGroup : SystemGroup
{
    public UiSystemGroup(
        EntityStore entities,
        Rid parentCanvas,
        IRectCanvas canvas,
        IPointerInputSource pointer,
        IOverlayToggleSource? debugToggle = null,
        IKeyboardState? keyboard = null,
        IVisualRidFactory? visuals = null,
        Vector2 screen = default,
        bool focus = true,
        bool tooltips = true) : base("Ui")
    {
        Scroll = new ScrollSystem();
        Add(new InitializationSystemGroup(parentCanvas));
        Add(new PresentationSystemGroup(canvas));
        Add(new UiInteractionSystemGroup(
            entities,
            pointer,
            Scroll,
            keyboard,
            visuals,
            screen,
            focus,
            tooltips));
        UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas,
            debugToggle);
    }

    public ScrollSystem Scroll { get; }
}
