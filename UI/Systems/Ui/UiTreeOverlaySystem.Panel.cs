using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>The F4 panel's tree: header, tools, search, the tree list and the inspector, docked right.</summary>
public sealed partial class UiTreeOverlaySystem
{
    private const float PanelWidth = 560;
    private const float RowHeight = 22;
    private const float ArrowWidth = 22;
    private const float SizeWidth = 90;
    private const float Indent = 14;
    private const float InspectorHeight = 300;
    private const float PropNameWidth = 150;
    private const float HintGap = 18;
    private const int RowType = UiType.Small;

    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    private Entity _layer;
    private UiNode _catcher;
    private Button _pick = null!;
    private Button _up = null!;
    private Button _all = null!;
    private Field _search = null!;
    private Label _path = null!;
    private PooledList _list = null!;
    private Label _foot = null!;
    private Label _inspectTitle = null!;
    private PooledList _propList = null!;
    private Column _hint = null!;
    private Label _hintText = null!;

    private void Open(EntityStore store)
    {
        UiNode ui = UiNode.Layer(store, default, UiLayers.DebugUiTree, _visuals, UiStage.Full);
        _layer = ui.Entity;
        _layer.AddTag<DebugUi>();

        // Built first so the panel paints over it: in pick mode it eats the click
        // that would otherwise press whatever is being picked.
        _catcher = ui.Rect(new Color(0, 0, 0, 0)).Cover().Input()
            .With(UiCommand.Of(UiTreeCommand.PickHere))
            .Hidden();

        Vector2 stage = UiStage.Full.Size;
        float height = stage.Y - Space.Lg * 2;
        ui.Add(new Column(gap: Space.Xs, pad: Space.Sm, cross: Cross.Stretch, tone: Tone.Solid,
            style: n => n.Input().Box(new Rect2(stage.X - Space.Lg - PanelWidth, Space.Lg, PanelWidth, height)))
        {
            new Row(gap: Space.Sm, cross: Cross.Center)
            {
                new Label("UI TREE", UiType.Section, ink: Ink.Accent),
                new Label("F4", UiType.Small, ink: Ink.Dim),
                new Spacer(),
                (_pick = new Button("PICK", UiType.Small, cmd: Cmd(UiTreeCommand.Pick),
                    tip: "point at anything on screen to see what it is; click it to open the tree there")),
                (_up = new Button("UP", UiType.Small, cmd: Cmd(UiTreeCommand.Up),
                    tip: "open the tree on the root's parent")),
                (_all = new Button("ALL", UiType.Small, cmd: Cmd(UiTreeCommand.All),
                    tip: "list every root: every layer and loose entity")),
            },
            new Row(gap: Space.Md)
            {
                Legend("■ BUTTON", DebugPalette.ButtonBorder),
                Legend("■ CONTAINER", DebugPalette.ContainerBorder),
                Legend("■ UNDER CURSOR", DebugPalette.HoveredBorder),
                Legend("■ INSPECTED", DebugPalette.SelectedBorder),
            },
            new Row(gap: Space.Sm, cross: Cross.Center)
            {
                new Label("FIND", UiType.Caption, ink: Ink.Dim),
                (_search = new Field(type: UiType.Small, height: Control.Xs, grow: 1,
                    tip: "type to list only the rows whose name has it, anywhere under the root")),
            },
            (_path = new Label("", UiType.Caption, ink: Ink.Dim, fit: true)),
            (_list = new PooledList(RowHeight, 0, changed: FillRows, grow: 1)
            {
                Row = _ =>
                {
                    var indent = new Box(width: 1, stretch: true);
                    var arrow = new Button("", RowType, tone: Tone.Quiet, cmd: Cmd(UiTreeCommand.Toggle, _rowParts.Count),
                        width: ArrowWidth, stretch: true);
                    var name = new Button("", RowType, tone: Tone.Quiet, cmd: Cmd(UiTreeCommand.Inspect, _rowParts.Count),
                        grow: 1, stretch: true)
                    {
                        Style = n => n.Align(TextAlign.Left).Fit(),
                    };
                    var size = new Label("", UiType.Caption, ink: Ink.Dim, align: Align.Right, width: SizeWidth, stretch: true);
                    _rowParts.Add(new RowParts(indent, arrow, name, size));
                    return [indent, arrow, name, size];
                },
            }),
            (_foot = new Label("", UiType.Caption, ink: Ink.Dim)),
            new Box(height: Border.Thin, tone: Tone.Quiet),
            (_inspectTitle = new Label("", UiType.Small, ink: Ink.Accent, fit: true)),
            (_propList = new PooledList(RowHeight - 4, 0, changed: FillProps, height: InspectorHeight)
            {
                Row = _ =>
                {
                    var name = new Label("", UiType.Caption, ink: Ink.Dim, width: PropNameWidth, fit: true);
                    var value = new Label("", UiType.Caption, grow: 1, fit: true);
                    _propRows.Add((name, value));
                    return [name, value];
                },
            }),
        });

        ui.Add(_hint = new Column(pad: Space.Xs, tone: Tone.Backing, free: true, hidden: true)
        {
            (_hintText = new Label("", UiType.Small)),
        });

        _listening = UiCommandEvents.OnPressed<UiTreeCommand>(store, (_, command, _) => _commands.Enqueue(command));
        _toTop = true;
        SetPicking(true);
    }

    /// <summary>A word in the colour of the boxes it names.</summary>
    private static Label Legend(string word, Color color) =>
        new(word, UiType.Caption) { Style = n => n.TextColor(color) };

    private void SetPicking(bool on)
    {
        _picking = on;
        UiLayout.SetHidden(_catcher, !on);
        _pick.Node.Chosen(on);
        if (!on)
            UiLayout.SetHidden(_hint, true);
    }

    /// <summary>In pick mode, the name of what is under the cursor, beside it.</summary>
    private void ShowPickHint(Entity hover)
    {
        bool show = _picking && !hover.IsNull;
        UiLayout.SetHidden(_hint, !show);
        if (!show)
            return;

        _hintText.Node.SetText($"{Name(hover)}   {SizeOf(hover)}   click to open");
        Vector2 at = Pointer();
        Vector2 size = _hint.Node.Entity.TryGetComponent(out Rectangle rect) ? rect.Bounds.Size : Vector2.Zero;
        Vector2 stage = UiStage.Full.Size;

        // Below right of the cursor, flipped where it would leave the screen.
        float x = at.X + HintGap + size.X > stage.X ? at.X - HintGap - size.X : at.X + HintGap;
        float y = at.Y + HintGap + size.Y > stage.Y ? at.Y - HintGap - size.Y : at.Y + HintGap;
        _hint.Node.At(new Vector2(x, y) + size / 2);
    }

    /// <summary>A pooled tree row's parts, by pooled index.</summary>
    private readonly record struct RowParts(Box Indent, Button Arrow, Button Name, Label Size);
}
