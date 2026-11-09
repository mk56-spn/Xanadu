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
    private GodotPointerInputSource _pointer = null!;
    private readonly KeyPressQueue _keys = new();
    private SvgAttachmentVisuals _attachments = null!;
    private Entity _session;
    private bool _fitted;
    /// <summary>
    /// Holds the grid, the art and the bone overlay, clipped to the preview, so
    /// no part of the character ever draws over the panels or their buttons.
    /// It is never cleared, so its clip stays put while its children redraw.
    /// </summary>
    private Rid _stage;
    private Rect2 _stageClip;
    private Rid _grid;
    private Rid _preview;
    private Rid _svgCanvas;
    private Rid _timelineCanvas;

    /// <summary>Set by <c>--shot=path</c>: capture the window there after <see cref="_shotAt"/> frames, then quit.</summary>
    private string? _shotPath;
    private bool _openLayers;
    private int _shotAt = 60;
    private int _frames;

    /// <summary>Set by <c>--shot-seconds=s</c>: capture once this much time has passed, in place of a frame count.</summary>
    private float _shotSeconds = -1f;
    private float _elapsed;

    /// <summary>Set by <c>--flow=from&gt;to</c>: open the FLOW tab on that hand-over and preview it.</summary>
    private string? _flow;

    /// <summary>Set by <c>--attacks</c>: start cycling the attacks on opening.</summary>
    private bool _attacks;

    /// <summary>Set by <c>--easing</c>: open the EASING window on opening.</summary>
    private bool _easing;

    /// <summary>Set by <c>--clip=name@seconds</c>: open that clip paused at that time.</summary>
    private string? _clip;

    /// <summary>Set by <c>--streak=bone</c>: hang a streak off that bone and hold the playhead where it shows.</summary>
    private string? _streak;

    /// <summary>Set by <c>--preview</c>: open in preview mode.</summary>
    private bool _startInPreview;

    /// <summary>Set by <c>--paths=bone,bone</c>: motion paths on, with those bones selected.</summary>
    private string? _paths;

    /// <summary>Set by <c>--dress=bone</c>: a dress hanging from that bone, selected.</summary>
    private string? _dress;
    private string? _cloth;
    private string? _svg;

    /// <summary>Set by <c>--paint=bone</c>: paint the weights of the SVG <c>--svg</c> selects, for that bone.</summary>
    private string? _paint;

    /// <summary>Set by <c>--plant=bone,bone</c>: plant those bones on opening.</summary>
    private string? _plant;

    /// <summary>Set by <c>--orbit=bone,bone</c>: send those bones round an ellipse on opening, unsaved.</summary>
    private string? _orbit;

    /// <summary>Set by <c>--zoom=x</c>: the preview's zoom once it has been fitted, to check what spills past its edges.</summary>
    private float _zoom = -1f;

    private VisualRidRelease _visuals = null!;

    public override void _Ready()
    {
        _store = new EntityStore();
        _visuals = new VisualRidRelease(_store);
        _root = new SystemRoot(_store);
        _pointer = new GodotPointerInputSource(this);
        var canvas = new GodotRectCanvas();

        _stage = CreateCanvas(AnimatorLayout.GridZ);
        _grid = CreateCanvas(0, _stage);
        _svgCanvas = CreateCanvas(AnimatorLayout.SvgZ - AnimatorLayout.GridZ, _stage);
        _preview = CreateCanvas(AnimatorLayout.PreviewZ - AnimatorLayout.GridZ, _stage);
        _timelineCanvas = CreateCanvas(AnimatorLayout.TimelineZ);
        _attachments = new SvgAttachmentVisuals(_svgCanvas, containLayers: true);

        UiTextBridge.Attach(this, _store);
        var toasts = new ToastSystem(_store, GetViewportRect().Size, new GodotVisualRidFactory());
        _root.Add(new UiSystemGroup(
            _store,
            GetCanvasItem(),
            canvas,
            _pointer,
            tooltips: false,
            wheel: _pointer));
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
            _keys,
            new GodotAnimatorClick(this),
            toasts));
        _root.Add(toasts);

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

        if (_session.GetComponent<AnimatorFile>().CloseRequested)
        {
            GetTree().Quit();
            return;
        }

        ClipStage();

        // Fit once the panels have been laid out, so the rig opens filling the preview.
        if (!_fitted && AnimatorPreviewCommands.Fit(_session))
        {
            _fitted = true;
            if (_zoom > 0f)
                _session.GetComponent<AnimatorPreviewView>().Zoom = _zoom;
        }

        _pointer.EndFrame();
        _frames++;
        if (_frames > 2)
            _elapsed += (float)delta;
        if (_frames == 2 && _flow is not null)
            StartFlow(_flow);
        if (_frames == 2 && _clip is not null)
            OpenClipAt(_clip);
        if (_frames == 2 && _easing)
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.ToggleEasingWindow));
        if (_frames == 2 && _attacks)
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.CycleAttacks));
        if (_frames == 2 && _streak is not null)
            AddStreak(_streak);
        if (_frames == 2 && _dress is not null)
        {
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectBone, _dress));
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.AddCloth));
        }
        if (_frames == 2 && _svg is not null)
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectAttachment, _svg));
        if (_frames == 2 && _paint is not null)
        {
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.PaintWeights));
            int bone = XafRig.FindBone(_session.GetComponent<AnimatorDocument>().Package, _paint);
            if (bone >= 0)
                AnimatorWeightCommands.PickBone(_session, bone);
        }
        if (_frames == 2 && _cloth is not null)
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectCloth, _cloth));
        if (_frames == 2 && _paths is not null)
            ShowPaths(_paths);
        if (_frames == 2 && _openLayers)
            _store.Query<UiTabs>().ToEntityList()[0].GetComponent<UiTabs>().Selected = AnimatorLayout.LayersTab;
        if (_frames == 2 && _plant is not null)
        {
            foreach (string bone in _plant.Split(','))
            {
                AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectBone, bone));
                AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.PlantBone));
            }
        }
        if (_frames == 2 && _orbit is not null)
        {
            foreach (string bone in _orbit.Split(','))
            {
                AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.SelectBone, bone));
                AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.LimbTarget, "orbit"));
            }
        }
        if (_frames == 2 && _startInPreview)
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.TogglePreviewMode));
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
    /// <c>--flow=from&gt;to</c>, <c>--streak=bone</c>, <c>--layers</c>, <c>--preview</c>, <c>--attacks</c>, <c>--easing</c>, <c>--clip=name@seconds</c> and
    /// <c>--paths=bone,bone</c> (motion paths on, those bones selected; empty for none),
    /// with <c>@clip</c> after the bones to open that clip paused at its middle,
    /// <c>--dress=bone</c> (adds a dress hanging from that bone and selects it)
    /// <c>--cloth=index</c> (selects an existing cloth, so its overlay shows) and
    /// <c>--svg=index</c> (selects an attachment, so its mesh outline shows).
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
            else if (argument.StartsWith("--zoom=", StringComparison.Ordinal) &&
                     float.TryParse(argument["--zoom=".Length..],
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out float zoom))
                _zoom = zoom;
            else if (argument.StartsWith("--flow=", StringComparison.Ordinal))
                _flow = argument["--flow=".Length..];
            else if (argument.StartsWith("--streak=", StringComparison.Ordinal))
                _streak = argument["--streak=".Length..];
            else if (argument == "--layers")
                _openLayers = true;
            else if (argument.StartsWith("--plant=", StringComparison.Ordinal))
                _plant = argument["--plant=".Length..];
            else if (argument.StartsWith("--orbit=", StringComparison.Ordinal))
                _orbit = argument["--orbit=".Length..];
            else if (argument.StartsWith("--clip=", StringComparison.Ordinal))
                _clip = argument["--clip=".Length..];
            else if (argument == "--easing")
                _easing = true;
            else if (argument == "--attacks")
                _attacks = true;
            else if (argument == "--preview")
                _startInPreview = true;
            else if (argument.StartsWith("--paths=", StringComparison.Ordinal))
                _paths = argument["--paths=".Length..];
            else if (argument.StartsWith("--dress=", StringComparison.Ordinal))
                _dress = argument["--dress=".Length..];
            else if (argument.StartsWith("--svg=", StringComparison.Ordinal))
                _svg = argument["--svg=".Length..];
            else if (argument.StartsWith("--paint=", StringComparison.Ordinal))
                _paint = argument["--paint=".Length..];
            else if (argument.StartsWith("--cloth=", StringComparison.Ordinal))
                _cloth = argument["--cloth=".Length..];
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

    private void OpenClipAt(string spec)
    {
        string[] parts = spec.Split('@');
        AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.Clip, parts[0]));
        AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
        runtime.SetPaused(true);
        if (parts.Length == 2 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float time))
            runtime.Seek(time);
    }

    private void ShowPaths(string bones)
    {
        _session.GetComponent<AnimatorModes>().MotionPaths = true;
        string[] parts = bones.Split('@');
        if (parts.Length == 2)
        {
            AnimatorCommands.Run(_session, new ToolAction(ToolActionKind.Clip, parts[1]));
            AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
            runtime.SetPaused(true);
            runtime.Seek(runtime.CurrentDuration / 2);
        }

        XafPackage package = _session.GetComponent<AnimatorDocument>().Package;
        string[] ids = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < ids.Length; i++)
        {
            int bone = XafRig.FindBone(package, ids[i]);
            if (bone < 0)
                continue;
            ref AnimatorSelection selection = ref _session.GetComponent<AnimatorSelection>();
            if (i == 0)
                selection.SelectOnlyBone(bone);
            else
                selection.ToggleBone(bone);
        }
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

    public override void _Input(InputEvent @event) => _pointer.Feed(@event);

    public override void _UnhandledInput(InputEvent @event) => _keys.Feed(@event);

    public override void _ExitTree()
    {
        _attachments.Release();
        _visuals.Dispose();
        foreach (Rid canvas in new[] { _grid, _preview, _svgCanvas, _timelineCanvas, _stage })
            RenderingServer.FreeRid(canvas);
    }

    /// <summary>Keeps the stage's clip on the preview as the layout moves it (preview mode, resizes).</summary>
    private void ClipStage()
    {
        Rect2 area = AnimatorGeometry.PanelArea(_session.GetComponent<AnimatorUi>().Preview);
        if (area == _stageClip)
            return;

        _stageClip = area;
        RenderingServer.CanvasItemSetCustomRect(_stage, true, area);
        RenderingServer.CanvasItemSetClip(_stage, true);
    }

    /// <summary>A canvas under <paramref name="parent"/> (the tool's own item by default); z is relative to it.</summary>
    private Rid CreateCanvas(int zIndex, Rid parent = default)
    {
        Rid canvas = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(canvas, parent.IsValid ? parent : GetCanvasItem());
        RenderingServer.CanvasItemSetZIndex(canvas, zIndex);
        return canvas;
    }
}
