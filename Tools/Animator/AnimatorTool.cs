using System;
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

    /// <summary>Set by <c>--shot=path</c>: capture the window there after <see cref="_shotAt"/> frames, then quit.</summary>
    private string? _shotPath;
    private int _shotAt = 60;
    private int _frames;

    /// <summary>Set by <c>--shot-seconds=s</c>: capture once this much time has passed, in place of a frame count.</summary>
    private float _shotSeconds = -1f;
    private float _elapsed;

    /// <summary>Set by <c>--flow=from&gt;to</c>: open the FLOW tab on that hand-over and preview it.</summary>
    private string? _flow;

    /// <summary>Set by <c>--streak=bone</c>: hang a streak off that bone and hold the playhead where it shows.</summary>
    private string? _streak;

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
        UiIcons.Loader = UiIcons.FromResources;
        _session.AddComponent(AnimatorLayout.Build(_store, GetViewportRect().Size));
        ReadArguments();
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
        _frames++;
        if (_frames > 2)
            _elapsed += (float)delta;
        if (_frames == 2 && _flow is not null)
            StartFlow(_flow);
        if (_frames == 2 && _streak is not null)
            AddStreak(_streak);
        bool due = _shotSeconds >= 0f ? _elapsed >= _shotSeconds : _frames == _shotAt;
        if (_shotPath is not null && due)
        {
            CaptureShot(_shotPath);
            _shotPath = null;
        }
    }

    /// <summary>
    /// Reads the capture flags, so an agent can check the tool without a
    /// desktop capture: <c>--shot=path</c>, <c>--shot-frames=n</c> or
    /// <c>--shot-seconds=s</c> (counted from the flow preview's start), and
    /// <c>--flow=from&gt;to</c> and <c>--streak=bone</c>.
    /// </summary>
    private void ReadArguments()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--shot=", StringComparison.Ordinal))
                _shotPath = argument["--shot=".Length..];
            else if (argument.StartsWith("--shot-frames=", StringComparison.Ordinal) &&
                     int.TryParse(argument["--shot-frames=".Length..], out int frames))
                _shotAt = Math.Max(2, frames);
            else if (argument.StartsWith("--shot-seconds=", StringComparison.Ordinal) &&
                     float.TryParse(argument["--shot-seconds=".Length..],
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out float seconds))
                _shotSeconds = Math.Max(0f, seconds);
            else if (argument.StartsWith("--flow=", StringComparison.Ordinal))
                _flow = argument["--flow=".Length..];
            else if (argument.StartsWith("--streak=", StringComparison.Ordinal))
                _streak = argument["--streak=".Length..];
        }
    }

    private void StartFlow(string flow)
    {
        string[] clips = flow.Split('>');
        if (clips.Length != 2)
            return;

        foreach (Entity strip in _store.Query<UiTabs>().ToEntityList())
            strip.GetComponent<UiTabs>().Selected = 2;
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.Clip, clips[0]));
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectTransition, clips[1]));
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.PreviewTransition));
    }

    private void AddStreak(string bone)
    {
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectBone, bone));
        AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
        runtime.SetPaused(true);
        runtime.Seek(0f);
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.AddEffect));
        _session.GetComponent<AnimatorDocument>().Runtime.Seek(0.05f);
    }

    private void CaptureShot(string path)
    {
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(path);
        if (error == Error.Ok)
            GD.Print($"Wrote {path}");
        else
            GD.PrintErr($"Could not write {path}: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
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
