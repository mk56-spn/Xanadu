using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Systems.Ui;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Files;

namespace Writer.Tools.Animator;

/// <summary>Standalone XAF skeleton/animation authoring tool: hosts the ECS and feeds it input.</summary>
public partial class AnimatorTool : Control
{
    private const string DefaultPath = "res://Animations/Characters/contact.xaf";

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private AnimatorPointer _pointer = null!;
    private UiSystemGroup _ui = null!;
    private SvgAttachmentVisuals _attachments = null!;
    private Rid _preview;
    private Rid _svgCanvas;
    private Rid _timelineCanvas;

    public override void _Ready()
    {
        _store = new EntityStore();
        _store.OnEntityDelete += FreeVisual;
        _root = new SystemRoot(_store);
        _pointer = new AnimatorPointer();
        var canvas = new GodotRectCanvas();

        _svgCanvas = CreateCanvas(AnimatorLayout.SvgZ);
        _preview = CreateCanvas(AnimatorLayout.PreviewZ);
        _timelineCanvas = CreateCanvas(AnimatorLayout.TimelineZ);
        _attachments = new SvgAttachmentVisuals(_svgCanvas);

        UiTextBridge.Attach(this, _store);
        _ui = new UiSystemGroup(_store, GetCanvasItem(), canvas, _pointer);
        _root.Add(_ui);
        _root.Add(new AnimatorSystemGroup(
            canvas,
            _preview,
            _timelineCanvas,
            _attachments,
            new GodotVisualRidFactory(),
            _pointer,
            _pointer,
            new GodotKeyboardState()));

        var files = new GodotAnimatorFiles();
        Entity session = AnimatorSession.Create(
            _store,
            AnimatorFileCommands.LoadOrDefault(files, DefaultPath),
            files,
            DefaultPath);
        session.AddComponent(AnimatorLayout.Build(_store, GetViewportRect().Size));
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _root.Update(new UpdateTick(
            (float)delta,
            (float)Time.GetTicksMsec() / 1000f));
        _pointer.EndFrame();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            _pointer.Position = motion.Position;
            _pointer.Shift = motion.ShiftPressed;
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _pointer.Position = button.Position;
            _pointer.Pressed = button.Pressed;
            _pointer.Shift = button.ShiftPressed;
            if (button.Pressed)
                _pointer.ClickPending = true;
        }
        else if (@event is InputEventMouseButton
                 {
                     Pressed: true,
                     ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
                 } scroll)
        {
            _ui.Scroll.Request(scroll.Position, scroll.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
            _pointer.Position = scroll.Position;
            _pointer.Control = scroll.CtrlPressed || scroll.MetaPressed;
            _pointer.Wheel += scroll.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
        }
    }

    public override void _ExitTree()
    {
        _attachments.Release();
        _store.OnEntityDelete -= FreeVisual;
        foreach (Entity entity in _store.Query<VisualRid>().ToEntityList())
            RenderingServer.FreeRid(entity.GetComponent<VisualRid>().Value);
        foreach (Rid canvas in new[] { _preview, _svgCanvas, _timelineCanvas })
            RenderingServer.FreeRid(canvas);
    }

    private Rid CreateCanvas(int zIndex)
    {
        Rid canvas = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(canvas, GetCanvasItem());
        RenderingServer.CanvasItemSetZIndex(canvas, zIndex);
        return canvas;
    }

    private static void FreeVisual(EntityDelete deleted)
    {
        if (deleted.Entity.TryGetComponent(out VisualRid visual))
            RenderingServer.FreeRid(visual.Value);
    }
}
