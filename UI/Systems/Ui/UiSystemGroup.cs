using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
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
        Handle parentCanvas,
        IRectCanvas canvas,
        IPointerInputSource pointer,
        IOverlayToggleSource? debugToggle = null,
        IKeyboardState? keyboard = null,
        IVisualRidFactory? visuals = null,
        Vector2 screen = default,
        bool focus = true,
        bool tooltips = true,
        IWheelSource? wheel = null) : base("Ui")
    {
        Add(new InitializationSystemGroup(parentCanvas));
        Add(new PresentationSystemGroup(canvas));
        Add(new UiInteractionSystemGroup(
            entities,
            pointer,
            new ScrollSystem(pointer, wheel),
            keyboard,
            visuals,
            screen,
            focus,
            tooltips));
        // Text editors: their input, then drawing what it left.
        Add(new TextEditorInputSystem(pointer, keyboard ?? HostPlatform.Keyboard, wheel));
        Add(new TextEditorDrawSystem(canvas));
        UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas,
            debugToggle);
    }
}
