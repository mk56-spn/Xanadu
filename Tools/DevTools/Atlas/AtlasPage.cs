using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;
using static Xanadu.Ui.Tree.UiSpecs;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas's tree, built once: the header, the list, and the three chart views, each column a pooled list.</summary>
internal sealed partial class AtlasStage
{
    private const float ListWidth = 420;

    /// <summary>The margin left of each list card, where how many types use it goes.</summary>
    private const float UsedWidth = 40;

    private const float CardHeight = Control.Xs;
    private const float CardGap = Space.Xs;
    private const float BigCardWidth = 300;

    /// <summary>A FOCUS side column: what points at the type, or what it points at.</summary>
    private const float SideWidth = 380;

    private const float CompareCardWidth = 240;
    private const float SimilarCardWidth = 300;

    /// <summary>A SIMILAR row with a type picked shows one card, so it gets the room of both.</summary>
    private const float SimilarPickedWidth = 380;

    private const float ScoreWidth = 60;
    private const float BreakdownWidth = 300;
    private const float MeterWidth = 110;
    private const float BudgetLabelWidth = 110;
    private const float BudgetCountWidth = 80;

    /// <summary>Atlas's most repeated shape: take the room, children stretched across.</summary>
    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    // Header.
    private readonly Button[] _views = new Button[3];
    private readonly List<KindChip> _kinds = new();
    private readonly Button[] _sorts = new Button[Sorts.Length];
    private Field _search = null!;

    // The list.
    private PooledList _list = null!;
    private Label _listFoot = null!;
    private readonly List<(Label Used, TypeCard Card)> _listRows = new();

    // The chart's views, and what it says when it has nothing to show.
    private Row _focusView = null!;
    private Column _compareView = null!, _similarView = null!, _empty = null!;
    private Label _emptyText = null!;

    // FOCUS: a side each, of headings and cards, with a line per row to the centre.
    private readonly FocusSide _in = new(), _out = new();
    private Column _focusLines = null!;
    private BigCard _centre = null!;

    // COMPARE.
    private Label _compareTitle = null!, _onlyAHead = null!, _onlyBHead = null!;
    private readonly CompareColumn _onlyA = new(), _shared = new(), _onlyB = new();
    private Column _compareLines = null!;
    private BigCard _aCard = null!, _bCard = null!;
    private UiNode _directLine;

    // SIMILAR.
    private Label _similarTitle = null!, _similarFoot = null!;
    private PooledList _similarList = null!;
    private readonly List<SimilarRow> _similarRows = new();

    private void BuildPage(UiNode body)
    {
        body.Add(new Column(Fill, gap: Space.Md)
        {
            Header(),
            Bar(),
            new Row(Fill, gap: Space.Xl)
            {
                TypeList(),
                new Column(Fill) { Focus(), Compare(), Similar(), Empty() },
            },
        });

        // The lines join cards, so they are added once the cards exist; their
        // containers come first in each view, so the cards draw over their ends.
        JoinFocus();
        JoinCompare();
    }

    // ---- header ---------------------------------------------------------------------

    private Row Header()
    {
        (string assembly, int components, int tags) = _heaviest;
        string exe = assembly.Replace("Xanadu.", "");
        return new Row(gap: Space.Sm)
        {
            new Label("ATLAS", UiType.Heading, ink: Ink.Accent),
            (_views[0] = new Button("FOCUS", cmd: Cmd(AtlasCommand.View, (int)View.Focus), tip: "one type: what points at it, and what it points at")),
            (_views[1] = new Button("COMPARE", cmd: Cmd(AtlasCommand.View, (int)View.Compare),
                tip: "two types side by side: what each touches alone, and what both touch; the list then picks the second")),
            (_views[2] = new Button("SIMILAR", cmd: Cmd(AtlasCommand.View, (int)View.Similar),
                tip: "types that could be one: same field types, same users and same uses, among the kinds the top bar shows; pick a type to see the ones like it")),
            new Button("BACK", tone: Tone.Secondary, cmd: Cmd(AtlasCommand.Back), tip: "back to the last type"),
            Enum.GetValues<NodeKind>().Select(kind => Remember(_kinds, new KindChip(kind))),
            new Spacer(),
            new Column(gap: Space.Xs)
            {
                Budget("COMPONENTS", components, CodeMap.MaxComponents, exe, "component types; one more than the limit makes CreateEntity throw"),
                Budget("TAGS", tags, CodeMap.MaxTags, exe, "tag types; past the limit tags silently share bits and queries go wrong"),
            },
        };
    }

    /// <summary>
    /// One of the ECS's hard limits: types of one sort in the heaviest exe against
    /// Friflo's 255. Amber past three quarters, red past nine tenths.
    /// </summary>
    private static Row Budget(string label, int count, int max, string exe, string why) =>
        new(gap: Space.Sm, tip: $"{count} {why}. Counted for {exe} with what it references, the heaviest exe, Friflo's own included; the limit is per process.")
        {
            new Label(label, UiType.Small, ink: Ink.Dim, width: BudgetLabelWidth),
            new Meter((float)count / max, size: new Vector2(MeterWidth, Space.Sm)),
            new Label($"{count} / {max}", UiType.Small, width: BudgetCountWidth),
        };

    private Row Bar()
    {
        string built = string.Join("  ·  ", _map.Assemblies.Select(a => $"{a.Name.Replace("Xanadu.", "")} {a.Built:HH:mm}"));
        return new Row(gap: Space.Sm)
        {
            (_search = new Field(size: new Vector2(ListWidth, Control.Sm), tip: "type to find a type; Escape clears it")),
            new Label("SORT", UiType.Small, ink: Ink.Dim),
            // Built with the arrow the chosen sort shows, so the measured box fits it.
            Sorts.Select((sort, i) => _sorts[i] = new Button(sort.Label + " ▼", UiType.Small, cmd: Cmd(AtlasCommand.Sort, (int)sort.Mode))),
            new Label($"{_map.Nodes.Count} types from the last builds: {built}", UiType.Small, ink: Ink.Dim, grow: 1, fit: true, tip: built),
        };
    }

    // ---- the list -------------------------------------------------------------------

    private Column TypeList() => new(width: ListWidth, cross: Cross.Stretch, gap: Space.Xs)
    {
        (_list = new PooledList(CardHeight, CardGap, changed: FillList, grow: 1)
        {
            Row = _ =>
            {
                var used = new Label("", UiType.Small, ink: Ink.Dim, align: Align.Right, width: UsedWidth, stretch: true);
                var card = new TypeCard(AtlasCommand.Pick, grow: 1);
                _listRows.Add((used, card));
                return [used, card];
            },
        }),
        (_listFoot = new Label("", UiType.Caption, ink: Ink.Dim)),
    };

    // ---- FOCUS ----------------------------------------------------------------------

    private Row Focus() => _focusView = new Row(Fill)
    {
        (_focusLines = new Column(free: true)),
        Side(_in, incoming: true),
        new Spacer(),
        new Column(width: BigCardWidth, stretch: true, main: Main.Center) { (_centre = new BigCard(BigCardWidth)) },
        new Spacer(),
        Side(_out, incoming: false),
    };

    private Column Side(FocusSide side, bool incoming) => new(width: SideWidth, cross: Cross.Stretch, gap: Space.Xs)
    {
        (side.Empty = new Label("", UiType.Body, ink: Ink.Dim, align: Align.Center, stretch: true)),
        (side.List = new PooledList(CardHeight, CardGap, changed: () => FillSide(side, incoming), grow: 1)
        {
            Row = _ =>
            {
                var heading = new Label("", UiType.Small, grow: 1, stretch: true);
                var card = new TypeCard(AtlasCommand.Go, grow: 1);
                side.Rows.Add((heading, card));
                return [heading, card];
            },
        }),
        (side.Foot = new Label("", UiType.Caption, ink: Ink.Dim)),
    };

    private void JoinFocus()
    {
        foreach (FocusSide side in new[] { _in, _out })
        {
            foreach ((_, TypeCard card) in side.Rows)
                side.Lines.Add(_focusLines.Node.Add(new Connector(card, _centre, 0)));
        }
    }

    // ---- COMPARE --------------------------------------------------------------------

    private Column Compare() => _compareView = new Column(Fill, gap: Space.Sm, hidden: true)
    {
        (_compareTitle = new Label("", UiType.Label, align: Align.Center, stretch: true, fit: true)),
        new Row(Fill, gap: Space.Xl)
            {
                (_compareLines = new Column(free: true)),
                ComparePart(_onlyA, AtlasCommand.Go, _onlyAHead = Heading()),
                new Column(width: CompareCardWidth, stretch: true, main: Main.Center, gap: Space.Md)
                {
                    (_aCard = new BigCard(CompareCardWidth)),
                    new Button("SWAP", tone: Tone.Secondary, stretch: true, cmd: Cmd(AtlasCommand.Swap), tip: "swap the two"),
                },
                ComparePart(_shared, AtlasCommand.Go, new Label("BOTH", UiType.Small, ink: Ink.Accent)),
                new Column(width: CompareCardWidth, stretch: true, main: Main.Center) { (_bCard = new BigCard(CompareCardWidth)) },
                ComparePart(_onlyB, AtlasCommand.GoSecond, _onlyBHead = Heading()),
            },
    };

    private static Label Heading() => new("", UiType.Small, ink: Ink.Dim, fit: true);

    private Column ComparePart(CompareColumn column, AtlasCommand press, Label heading) => new(Fill, gap: Space.Xs)
    {
        heading,
        (column.List = new PooledList(CardHeight, CardGap, changed: () => FillCompare(column), grow: 1)
        {
            Row = _ => [Remember(column.Rows, new TypeCard(press, grow: 1))],
        }),
        (column.Foot = new Label("", UiType.Caption, ink: Ink.Dim)),
    };

    private void JoinCompare()
    {
        UiNode lines = _compareLines;
        _directLine = lines.Add(new Connector(_aCard, _bCard, 0));
        foreach (TypeCard card in _onlyA.Rows)
            _onlyA.ToA.Add(lines.Add(new Connector(card, _aCard, 0)));
        foreach (TypeCard card in _shared.Rows)
        {
            _shared.ToA.Add(lines.Add(new Connector(card, _aCard, 0)));
            _shared.ToB.Add(lines.Add(new Connector(card, _bCard, 0)));
        }
        foreach (TypeCard card in _onlyB.Rows)
            _onlyB.ToB.Add(lines.Add(new Connector(card, _bCard, 0)));
    }

    // ---- SIMILAR --------------------------------------------------------------------

    private Column Similar() => _similarView = new Column(Fill, gap: Space.Sm, hidden: true)
    {
        (_similarTitle = new Label("", UiType.Label, stretch: true, fit: true)),
            (_similarList = new PooledList(CardHeight, CardGap, changed: FillSimilar, grow: 1)
            {
                Row = _ =>
                {
                    var row = new SimilarRow(
                        new TypeCard(AtlasCommand.Pair, width: SimilarCardWidth),
                        new TypeCard(AtlasCommand.Pair, width: SimilarCardWidth),
                        new Label("", UiType.Body, width: ScoreWidth),
                        new Label("", UiType.Small, ink: Ink.Dim, width: BreakdownWidth, fit: true),
                        new Label("", UiType.Small, ink: Ink.Dim, grow: 1, fit: true));
                    _similarRows.Add(row);
                    return [row.A, row.B, row.Score, row.Breakdown, row.Fields];
                },
            }),
            (_similarFoot = new Label("", UiType.Caption, ink: Ink.Dim)),
    };

    private Column Empty() => _empty = new Column(grow: 1, main: Main.Center)
    {
        (_emptyText = new Label("", UiType.Section, ink: Ink.Dim, align: Align.Center, stretch: true)),
    };

    private static T Remember<T>(List<T> into, T item)
    {
        into.Add(item);
        return item;
    }
}
