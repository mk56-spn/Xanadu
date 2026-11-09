using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Theme;

namespace Xanadu.Lab;

/// <summary>
/// Standalone sandbox for isolated experiments: a tab per experiment, its notes
/// and controls in an ECS side panel, its Godot drawing on the stage.
/// </summary>
public partial class LabHost : Control
{
    private const float Margin = UiMetrics.Space.Xl;
    private const float PanelWidth = 640;
    private const float TabWidth = 240;

    /// <summary>The global shader uniform carrying the pointer; declared in project.godot.</summary>
    private static readonly StringName MouseUniform = "lab_mouse";

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    /// <summary>Frames before a <c>--shot</c> capture: long enough for timings to settle and report. <c>--shot-frame=N</c> overrides it.</summary>
    private int _shotFrame = 120;

    /// <summary>The pointer in canvas pixels, as last sent to <c>lab_mouse</c>; for stages that need it on the CPU.</summary>
    public static Vector2 Mouse { get; private set; }

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private GodotPointerInputSource _pointer = null!;
    private VisualRidRelease _visuals = null!;
    private LabSettings _settings = null!;
    private DebugSystemGroup _debug = null!;
    private ILabExperiment[] _experiments = [];
    /// <summary>The open tab's stage; the others do not exist, so nothing of theirs runs or holds GPU memory.</summary>
    private Node2D? _stage;
    private Rect2 _area;
    private Entity _strip;
    private int _shown = -1;

    /// <summary>Set by <c>--shot=path</c>: capture the window after a few frames, then quit.</summary>
    private string? _shotPath;
    private int _frames;

    /// <summary>Set by <c>--mouse=x,y</c>: pins the pointer uniform, for repeatable shots and timings.</summary>
    private Vector2? _mouse;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(Palette.Background);
        _store = new EntityStore();
        _visuals = new VisualRidRelease(_store);
        _root = new SystemRoot(_store);
        _pointer = new GodotPointerInputSource(this);
        _root.Add(new UiSystemGroup(
            _store,
            GetCanvasItem(),
            new GodotRectCanvas(),
            _pointer,
            wheel: _pointer));
        // The same F1 frame rate, F3 ECS/system metrics and F4 UI overlays as the game.
        _debug = new DebugSystemGroup(
            new GodotRectCanvas(),
            GetCanvasItem(),
            new KeyOverlayToggleSource(DebugKeys.Ecs),
            _store,
            _root,
            new GodotFpsSource(),
            new KeyOverlayPageSource());
        _root.Add(_debug);

        _experiments = LabExperiments.All();
        Build();
        ReadArguments();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        // Every experiment's shaders can read the pointer, in canvas pixels.
        Mouse = _mouse ?? GetGlobalMousePosition();
        RenderingServer.GlobalShaderParameterSet(MouseUniform, Mouse);
        _root.Update(new UpdateTick((float)delta, (float)Time.GetTicksMsec() / 1000f));
        _pointer.EndFrame();
        _settings.Poll();
        ShowSelected();

        if (_shotPath is not null && ++_frames == _shotFrame)
        {
            GetViewport().GetTexture().GetImage().SavePng(_shotPath);
            GetTree().Quit();
        }
    }

    public override void _Input(InputEvent @event) => _pointer.Feed(@event);

    public override void _ExitTree()
    {
        _debug.Dispose();
        _visuals.Dispose();
    }

    private void Build()
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(_store, default, UiLayers.Base, null, stage);

        float tabsY = Margin + UiMetrics.Control.Lg / 2;
        float bodyTop = Margin * 2 + UiMetrics.Control.Lg;
        var body = new Rect2(Margin, bodyTop, PanelWidth, stage.Height - bodyTop - Margin);
        float stageLeft = body.End.X + Margin;
        var area = _area = new Rect2(stageLeft, bodyTop, stage.Width - stageLeft - Margin, body.Size.Y);

        UiNode strip = ui.Row().Gap(UiMetrics.Space.Sm).AnchorLeft().At(new Vector2(Margin, tabsY));
        _strip = strip;
        var buttons = new Entity[_experiments.Length];
        var bodies = new Entity[_experiments.Length];
        // Tabs shrink once there are too many to fit the strip at full width; the
        // settings button at the end takes a tab's width.
        int tabs = _experiments.Length + 1;
        float tabWidth = Math.Min(TabWidth, (stage.Width - Margin * 2 - UiMetrics.Space.Sm * (tabs - 1)) / tabs);

        ui.Rect(Palette.SurfaceDeep).Box(area).Border(Palette.BorderStrong);

        for (int i = 0; i < _experiments.Length; i++)
        {
            ILabExperiment experiment = _experiments[i];
            buttons[i] = strip.Button(experiment.Name, UiType.Label)
                .Size(tabWidth, UiMetrics.Control.Lg);

            UiNode panel = ui.Panel().Size(body.Size).At(body.GetCenter()).Hidden();
            panel.Text(experiment.Name, UiType.Section, Palette.Accent);
            foreach (string line in experiment.Notes)
                panel.Text(line, UiType.Small, Palette.TextMuted);
            experiment.BuildPanel(panel);
            if (experiment.SourcePath is { } source)
                LabSourceView.Build(panel, source, SourceSize(experiment, body.Size));
            bodies[i] = panel;
        }

        _strip.AddComponent(new UiTabs(buttons, bodies, body.GetCenter()));

        UiNode settings = strip.Button("SETTINGS", UiType.Label).Size(tabWidth, UiMetrics.Control.Lg);
        _settings = new LabSettings(_store, GetViewport(), body, settings);
    }

    /// <summary>The listing fills what the title and notes leave of the panel.</summary>
    private static Vector2 SourceSize(ILabExperiment experiment, Vector2 body)
    {
        float used = UiNode.Measure(experiment.Name, UiType.Section).Y
                     + experiment.Notes.Length * UiNode.Measure("A", UiType.Small).Y
                     + LabSourceView.HeaderHeight
                     + experiment.ControlsHeight
                     + (experiment.Notes.Length + 2) * UiTheme.ContainerSpacing
                     + UiTheme.ContainerPadding * 2;
        return new Vector2(body.X - UiTheme.ContainerPadding * 2, body.Y - used);
    }

    /// <summary>
    /// The tab strip owns the selection; the stage follows it. Leaving a tab frees its
    /// stage and opening one builds it fresh, so experiments start clean every time.
    /// </summary>
    private void ShowSelected()
    {
        int selected = _strip.GetComponent<UiTabs>().Selected;
        if (selected == _shown)
            return;

        _stage?.QueueFree();
        _stage = _experiments[selected].CreateStage(_area);
        _stage.ZIndex = StageZ;
        AddChild(_stage);
        _shown = selected;
    }

    private void ReadArguments()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shot=", StringComparison.Ordinal))
                _shotPath = arg["--shot=".Length..];
            else if (arg.StartsWith("--shot-frame=", StringComparison.Ordinal)
                     && int.TryParse(arg["--shot-frame=".Length..], out int frame) && frame > 0)
                _shotFrame = frame;
            else if (arg.StartsWith("--mouse=", StringComparison.Ordinal)
                     && arg["--mouse=".Length..].Split(',') is [var x, var y]
                     && float.TryParse(x, out float mx) && float.TryParse(y, out float my))
                _mouse = new Vector2(mx, my);
            else if (arg.StartsWith("--tab=", StringComparison.Ordinal)
                     && int.TryParse(arg["--tab=".Length..], out int tab)
                     && tab >= 0 && tab < _experiments.Length)
                _strip.GetComponent<UiTabs>().Selected = tab;
        }
    }
}
