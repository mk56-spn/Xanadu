using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Theme;

namespace Xanadu.Labs;

/// <summary>
/// Xanadu Labs on the MoonWorks host: a tab per experiment, its notes and
/// controls in an ECS side panel, its drawing on the stage to the right. The
/// same shell as the Godot lab (Tools/Lab), whose experiments are rebuilt here
/// as they're wanted.
/// </summary>
internal sealed class LabsApp : HostApp
{
    private const float Margin = UiMetrics.Space.Xl;
    private const float PanelWidth = 640;
    private const float TabWidth = 240;

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    private readonly SystemRoot _root;
    private readonly DebugSystemGroup _debug;
    private readonly ILabExperiment[] _experiments;
    private Entity _strip;
    private Rect2 _area;
    private int _shown = -1;
    private ILabStage? _stage;
    private Rid _stageItem;

    public LabsApp(LabsOptions options) : base("Xanadu Labs",
        new HostWindowOptions(options.WindowWidth, options.WindowHeight, options.ShotPath, options.ShotFrame))
    {
        _root = new SystemRoot(Entities);
        _root.Add(new UiSystemGroup(
            Entities,
            Canvas.RootRid,
            HostPlatform.NewCanvas(),
            Pointer,
            keyboard: Keyboard,
            wheel: Pointer));
        // The same F1 frame rate, F3 ECS/system metrics and F4 UI overlays as the game.
        _debug = new DebugSystemGroup(
            HostPlatform.NewCanvas(),
            Canvas.RootRid,
            new KeyOverlayToggleSource(DebugKeys.Ecs),
            Entities,
            _root,
            Fps,
            new KeyOverlayPageSource());
        _root.Add(_debug);

        _experiments = LabExperiments.All(options);
        Build();
        if (options.Tab >= 0 && options.Tab < _experiments.Length)
            _strip.GetComponent<UiTabs>().Selected = options.Tab;
    }

    private void Build()
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(Entities, default, UiLayers.Base, null, stage);

        float tabsY = Margin + UiMetrics.Control.Lg / 2;
        float bodyTop = Margin * 2 + UiMetrics.Control.Lg;
        var body = new Rect2(Margin, bodyTop, PanelWidth, stage.Height - bodyTop - Margin);
        float stageLeft = body.End.X + Margin;
        _area = new Rect2(stageLeft, bodyTop, stage.Width - stageLeft - Margin, body.Size.Y);

        UiNode strip = ui.Row().Gap(UiMetrics.Space.Sm).AnchorLeft().At(new Vector2(Margin, tabsY));
        _strip = strip;
        var buttons = new Entity[_experiments.Length];
        var bodies = new Entity[_experiments.Length];
        ui.Rect(Palette.SurfaceDeep).Box(_area).Border(Palette.BorderStrong);

        for (int i = 0; i < _experiments.Length; i++)
        {
            ILabExperiment experiment = _experiments[i];
            buttons[i] = strip.Button(experiment.Name, UiType.Label).Size(TabWidth, UiMetrics.Control.Lg);

            UiNode panel = ui.Panel().Size(body.Size).At(body.GetCenter()).Hidden();
            panel.Text(experiment.Name, UiType.Section, Palette.Accent);
            foreach (string line in experiment.Notes)
                panel.Text(line, UiType.Small, Palette.TextMuted);
            experiment.BuildPanel(panel);
            bodies[i] = panel;
        }

        _strip.AddComponent(new UiTabs(buttons, bodies, body.GetCenter()));
    }

    protected override void Tick(float delta, float time)
    {
        _root.Update(new UpdateTick(delta, time));
        ShowSelected();
        _stage?.Update(new LabFrame(delta, time, Pointer.Position));
    }

    /// <summary>
    /// The tab strip owns the selection; the stage follows it. Leaving a tab frees
    /// its stage and opening one builds it fresh, so experiments start clean.
    /// </summary>
    private void ShowSelected()
    {
        int selected = _strip.GetComponent<UiTabs>().Selected;
        if (selected == _shown)
            return;

        _stage?.Dispose();
        if (_stageItem.IsValid)
            CanvasServer.Current.Free(_stageItem);

        _stageItem = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_stageItem, Canvas.RootRid);
        CanvasServer.Current.SetZIndex(_stageItem, StageZ);
        _stage = _experiments[selected].CreateStage(new LabContext(Renderer.Pipelines), _stageItem, _area);
        _shown = selected;
    }

    protected override void Shutdown()
    {
        _stage?.Dispose();
        _debug.Dispose();
    }
}
