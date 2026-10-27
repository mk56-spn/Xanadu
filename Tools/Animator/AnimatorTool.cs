using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator;

/// <summary>Standalone XAF skeleton/animation authoring tool: hosts the ECS and feeds it input.</summary>
public partial class AnimatorTool : Control
{
    private const string DefaultPath = "res://Animations/Characters/contact.xaf";

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private AnimatorPointer _pointer = null!;
    private UiSystemGroup _ui = null!;
    private SvgAttachmentVisuals _attachments = null!;
    private Entity _session;
    private bool _fitted;
    private Rid _grid;
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

        _grid = CreateCanvas(AnimatorLayout.GridZ);
        _svgCanvas = CreateCanvas(AnimatorLayout.SvgZ);
        _preview = CreateCanvas(AnimatorLayout.PreviewZ);
        _timelineCanvas = CreateCanvas(AnimatorLayout.TimelineZ);
        _attachments = new SvgAttachmentVisuals(_svgCanvas);

        UiTextBridge.Attach(this, _store);
        _ui = new UiSystemGroup(
            _store,
            GetCanvasItem(),
            canvas,
            _pointer,
            tooltips: false);
        _root.Add(_ui);
        _root.Add(new AnimatorSystemGroup(
            canvas,
            _grid,
            _preview,
            _timelineCanvas,
            _attachments,
            new GodotVisualRidFactory(),
            new GodotAnimatorSvgCatalog(),
            _pointer,
            _pointer,
            new GodotKeyboardState()));

        var files = new GodotAnimatorFiles();
        _session = AnimatorSession.Create(
            _store,
            AnimatorFileCommands.LoadOrDefault(files, DefaultPath),
            files,
            DefaultPath);
        _session.AddComponent(AnimatorLayout.Build(_store, GetViewportRect().Size));
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _root.Update(new UpdateTick(
            (float)delta,
            (float)Time.GetTicksMsec() / 1000f));

        // Fit once the panels have been laid out, so the rig opens filling the preview.
        if (!_fitted && AnimatorPreviewCommands.Fit(_session))
            _fitted = true;

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
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            _pointer.Position = right.Position;
            _pointer.Secondary = right.Pressed;
            if (right.Pressed)
                _pointer.SecondaryPending = true;
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle)
        {
            _pointer.Position = middle.Position;
            _pointer.Middle = middle.Pressed;
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
        foreach (Rid canvas in new[] { _grid, _preview, _svgCanvas, _timelineCanvas })
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
