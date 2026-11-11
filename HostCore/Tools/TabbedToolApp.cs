using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Ui;
using Xanadu.Theme;

namespace Xanadu.Host.Tools;

// Reads the pointer outside UI (BannedSymbols.txt): the tool shell hands each tab's stage its frame, so tabs never read it.
#pragma warning disable RS0030

/// <summary>
/// What a tab's stage is handed each frame: the time, the pointer in design
/// pixels, whether its left button is down, the wheel notches the UI left
/// (positive is up), and the raw MoonWorks input for stages that want the
/// other buttons or keys.
/// </summary>
internal readonly record struct ToolFrame(float Delta, float Time, Vector2 Mouse, bool Held, MoonWorks.Input.Inputs? Inputs = null, int Wheel = 0);

/// <summary>
/// One tab's drawing. It is made when its tab first opens; when another tab
/// opens it is disposed, or only hidden if its tab keeps it (<see cref="IToolTab.Keep"/>).
/// </summary>
internal interface IToolStage : IDisposable
{
    void Update(in ToolFrame frame);

    /// <summary>Its tab opened: on first opening, and again each time a kept stage comes back.</summary>
    void Shown() { }

    /// <summary>A kept stage's tab closed; it stays as it is, out of sight, until shown again.</summary>
    void Hidden() { }

    /// <summary>Records the stage's own GPU passes, before the canvas draws (for stages that render offscreen).</summary>
    void Render(MoonWorks.Graphics.CommandBuffer commands) { }
}

/// <summary>A tab in a <see cref="TabbedToolApp"/>: its label, its side panel, and how much room its stage takes.</summary>
internal interface IToolTab
{
    /// <summary>The tab label.</summary>
    string Name { get; }

    /// <summary>True to drop the side panel and lay out the whole body (stage and panel room) itself.</summary>
    bool FullWidth => false;

    /// <summary>Short lines shown at the top of the side panel.</summary>
    string[] Notes { get; }

    /// <summary>Adds any controls or readouts the tab wants under its notes.</summary>
    void BuildPanel(UiNode panel) { }

    /// <summary>
    /// True to hide its stage, not free it, when another tab opens, so it is
    /// as it was left on coming back. False builds it fresh each time.
    /// </summary>
    bool Keep => false;
}

/// <summary>
/// The shell every tabbed tool shares (Labs, the dev tools): a strip of tabs
/// along the top, the open tab's notes and controls in an ECS side panel, and
/// its drawing on a stage to the right. Leaving a tab frees its stage and
/// opening one builds it fresh, so every tab starts clean, unless the tab
/// keeps it (<see cref="IToolTab.Keep"/>). A tool only says what its tabs are
/// and how to make a tab's stage, and can send the user to a tab (<see cref="Open"/>).
/// </summary>
internal abstract class TabbedToolApp : HostApp
{
    private const float Margin = UiMetrics.Space.Xl;
    private const float PanelWidth = 640;
    private const float MaxTabWidth = 200;

    /// <summary>The tab strip is kept slim: tools are often short of room, and the tabs only need to be read and hit.</summary>
    private const float TabHeight = UiMetrics.Control.Sm;

    /// <summary>The room above the tabs and between them and the body.</summary>
    private const float StripGap = UiMetrics.Space.Md;

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    private readonly IToolTab[] _tabs;
    private Entity _strip;
    private Entity _frame;
    private Rect2 _area;

    /// <summary>The panel's and the stage's room together, for tabs that lay out their own (<see cref="IToolTab.FullWidth"/>).</summary>
    private Rect2 _fullArea;
    private int _shown = -1;

    /// <summary>Each tab's stage and its canvas item, while it has one.</summary>
    private readonly IToolStage?[] _stages;
    private readonly Handle[] _items;

    private IToolStage? Stage => _shown >= 0 ? _stages[_shown] : null;

    protected TabbedToolApp(string title, HostWindowOptions window, IToolTab[] tabs, int openTab = 0) : base(title, window)
    {
        _tabs = tabs;
        _stages = new IToolStage?[tabs.Length];
        _items = new Handle[tabs.Length];
        Build();
        if (openTab >= 0 && openTab < _tabs.Length)
            _strip.GetComponent<UiTabs>().Selected = openTab;
    }

    /// <summary>Builds tab <paramref name="tab"/>'s stage under <paramref name="parent"/>, laid out inside <paramref name="area"/> (design pixels).</summary>
    protected abstract IToolStage CreateStage(int tab, Handle parent, Rect2 area);

    /// <summary>Opens a tab, as pressing it would; the switch happens at the start of next frame.</summary>
    protected void Open(int tab)
    {
        if (tab >= 0 && tab < _tabs.Length)
            _strip.GetComponent<UiTabs>().Selected = tab;
    }

    /// <summary>A tab's stage, if it has one now.</summary>
    protected IToolStage? StageOf(int tab) => tab >= 0 && tab < _stages.Length ? _stages[tab] : null;

    private void Build()
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(Entities, default, UiLayers.Base, null, stage);

        float tabsY = StripGap + TabHeight / 2;
        float bodyTop = StripGap * 2 + TabHeight;
        var body = new Rect2(Margin, bodyTop, PanelWidth, stage.Height - bodyTop - Margin);
        float stageLeft = body.End.X + Margin;
        _area = new Rect2(stageLeft, bodyTop, stage.Width - stageLeft - Margin, body.Size.Y);
        _fullArea = new Rect2(Margin, bodyTop, stage.Width - Margin * 2, body.Size.Y);
        // Every tab fits on the strip, however many there are.
        float gap = UiMetrics.Space.Sm;
        float tabWidth = MathF.Min(MaxTabWidth, (stage.Width - Margin * 2 - gap * (_tabs.Length - 1)) / _tabs.Length);

        UiNode strip = ui.Row().Gap(UiMetrics.Space.Sm).AnchorLeft().At(new Vector2(Margin, tabsY));
        _strip = strip;
        var buttons = new Entity[_tabs.Length];
        var bodies = new Entity[_tabs.Length];
        _frame = ui.Rect(Palette.SurfaceDeep).Box(_area).Border(Palette.BorderStrong);

        for (int i = 0; i < _tabs.Length; i++)
        {
            IToolTab tab = _tabs[i];
            buttons[i] = strip.Button(tab.Name, UiType.Small).Size(tabWidth, TabHeight);
            if (tab.FullWidth)
            {
                // Nothing to show beside the stage: an empty body keeps the tabs' bookkeeping.
                bodies[i] = ui.Column().At(body.GetCenter()).Hidden();
                continue;
            }

            UiNode panel = ui.Panel().Size(body.Size).At(body.GetCenter()).Hidden();
            panel.Text(tab.Name, UiType.Section, Palette.Accent);
            foreach (string line in tab.Notes)
                panel.Text(line, UiType.Small, Palette.TextMuted);
            tab.BuildPanel(panel);
            bodies[i] = panel;
        }

        _strip.AddComponent(new UiTabs(buttons, bodies, body.GetCenter()));
    }

    protected sealed override void Tick(float delta, float time)
    {
        ShowSelected();
        Stage?.Update(new ToolFrame(delta, time, Pointer.Position, Pointer.LeftDown, Inputs, Pointer.TakeWheel()));
    }

    /// <summary>The tab strip owns the selection; the stage follows it.</summary>
    private void ShowSelected()
    {
        int selected = _strip.GetComponent<UiTabs>().Selected;
        if (selected == _shown)
            return;

        if (_shown >= 0)
            Leave(_shown);

        _shown = selected;
        if (_stages[selected] is null)
        {
            _items[selected] = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_items[selected], Canvas.RootRid);
            CanvasServer.Current.SetZIndex(_items[selected], StageZ);
            _stages[selected] = CreateStage(selected, _items[selected], _tabs[selected].FullWidth ? _fullArea : _area);
        }
        else
        {
            CanvasServer.Current.SetVisible(_items[selected], true);
        }

        _stages[selected]!.Shown();
        // A full-width tab draws its own frame.
        UiLayout.SetHidden(_frame, _tabs[selected].FullWidth);
    }

    /// <summary>A kept tab's stage is hidden; any other is disposed and its item freed.</summary>
    private void Leave(int tab)
    {
        if (_stages[tab] is not { } stage)
            return;

        if (_tabs[tab].Keep)
        {
            stage.Hidden();
            CanvasServer.Current.SetVisible(_items[tab], false);
            return;
        }

        stage.Dispose();
        CanvasServer.Current.Free(_items[tab]);
        _stages[tab] = null;
        _items[tab] = default;
    }

    protected sealed override void RenderOffscreen(MoonWorks.Graphics.CommandBuffer commands) => Stage?.Render(commands);

    protected override void Shutdown()
    {
        foreach (IToolStage? stage in _stages)
            stage?.Dispose();
    }
}
