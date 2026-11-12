using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;

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
        body.Add(new Column
        {
            Grow = 1,
            Cross = StackCrossAlignment.Stretch,
            Gap = Space.Md,
            Children =
            {
                Header(),
                Bar(),
                new Row
                {
                    Grow = 1,
                    Cross = StackCrossAlignment.Stretch,
                    Gap = Space.Xl,
                    Children =
                    {
                        TypeList(),
                        new Column
                        {
                            Grow = 1,
                            Cross = StackCrossAlignment.Stretch,
                            Children = { Focus(), Compare(), Similar(), Empty() },
                        },
                    },
                },
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
        return new Row
        {
            Gap = Space.Sm,
            Children =
            {
                new Label("ATLAS", UiType.Heading) { Role = TextRole.Accent },
                (_views[(int)View.Focus] = ViewButton(View.Focus, "FOCUS", "one type: what points at it, and what it points at")),
                (_views[(int)View.Compare] = ViewButton(View.Compare, "COMPARE",
                    "two types side by side: what each touches alone, and what both touch; the list then picks the second")),
                (_views[(int)View.Similar] = ViewButton(View.Similar, "SIMILAR",
                    "types that could be one: same field types, same users and same uses, among the kinds the top bar shows; pick a type to see the ones like it")),
                new Button("BACK")
                {
                    Tone = Tone.Secondary,
                    Command = UiCommand.Of(AtlasCommand.Back),
                    Tooltip = "back to the last type",
                },
                Enum.GetValues<NodeKind>().Select(kind => Remember(_kinds, new KindChip(kind))),
                new Spacer(),
                new Column
                {
                    Gap = Space.Xs,
                    Children =
                    {
                        Budget("COMPONENTS", components, CodeMap.MaxComponents, exe, "component types; one more than the limit makes CreateEntity throw"),
                        Budget("TAGS", tags, CodeMap.MaxTags, exe, "tag types; past the limit tags silently share bits and queries go wrong"),
                    },
                },
            },
        };
    }

    private static Button ViewButton(View view, string label, string hint) =>
        new(label) { Command = UiCommand.Of(AtlasCommand.View, (int)view), Tooltip = hint };

    /// <summary>
    /// One of the ECS's hard limits: types of one sort in the heaviest exe against
    /// Friflo's 255. Amber past three quarters, red past nine tenths.
    /// </summary>
    private static Row Budget(string label, int count, int max, string exe, string why) => new()
    {
        Gap = Space.Sm,
        Tooltip = $"{count} {why}. Counted for {exe} with what it references, the heaviest exe, Friflo's own included; the limit is per process.",
        Children =
        {
            new Label(label, UiType.Small) { Role = TextRole.Dim, Width = BudgetLabelWidth },
            new Meter((float)count / max) { Size = new Vector2(MeterWidth, Space.Sm) },
            new Label($"{count} / {max}", UiType.Small) { Width = BudgetCountWidth },
        },
    };

    private Row Bar()
    {
        string built = string.Join("  ·  ", _map.Assemblies.Select(a => $"{a.Name.Replace("Xanadu.", "")} {a.Built:HH:mm}"));
        return new Row
        {
            Gap = Space.Sm,
            Children =
            {
                (_search = new Field { Size = new Vector2(ListWidth, Control.Sm), Tooltip = "type to find a type; Escape clears it" }),
                new Label("SORT", UiType.Small) { Role = TextRole.Dim },
                // Built with the arrow the chosen sort shows, so the measured box fits it.
                Sorts.Select((sort, i) => _sorts[i] = new Button(sort.Label + " ▼", UiType.Small)
                {
                    Command = UiCommand.Of(AtlasCommand.Sort, (int)sort.Mode),
                }),
                new Label($"{_map.Nodes.Count} types from the last builds: {built}", UiType.Small)
                {
                    Role = TextRole.Dim,
                    Grow = 1,
                    Fit = true,
                    Tooltip = built,
                },
            },
        };
    }

    // ---- the list -------------------------------------------------------------------

    private Column TypeList() => new()
    {
        Width = ListWidth,
        Cross = StackCrossAlignment.Stretch,
        Gap = Space.Xs,
        Children =
        {
            (_list = new PooledList(CardHeight, CardGap)
            {
                Grow = 1,
                Changed = FillList,
                Row = _ =>
                {
                    var used = new Label("", UiType.Small) { Role = TextRole.Dim, Align = TextAlign.Right, Width = UsedWidth, Stretch = true };
                    var card = new TypeCard(AtlasCommand.Pick) { Grow = 1 };
                    _listRows.Add((used, card));
                    return [used, card];
                },
            }),
            (_listFoot = new Label("", UiType.Caption) { Role = TextRole.Dim }),
        },
    };

    // ---- FOCUS ----------------------------------------------------------------------

    private Row Focus() => _focusView = new Row
    {
        Grow = 1,
        Cross = StackCrossAlignment.Stretch,
        Children =
        {
            (_focusLines = new Column { Free = true }),
            Side(_in, incoming: true),
            new Spacer(),
            new Column
            {
                Width = BigCardWidth,
                Stretch = true,
                Main = StackMainAlignment.Center,
                Children = { (_centre = new BigCard(BigCardWidth)) },
            },
            new Spacer(),
            Side(_out, incoming: false),
        },
    };

    private Column Side(FocusSide side, bool incoming) => new()
    {
        Width = SideWidth,
        Cross = StackCrossAlignment.Stretch,
        Gap = Space.Xs,
        Children =
        {
            (side.Empty = new Label("", UiType.Body) { Role = TextRole.Dim, Align = TextAlign.Center, Stretch = true }),
            (side.List = new PooledList(CardHeight, CardGap)
            {
                Grow = 1,
                Changed = () => FillSide(side, incoming),
                Row = _ =>
                {
                    var heading = new Label("", UiType.Small) { Grow = 1, Stretch = true };
                    var card = new TypeCard(AtlasCommand.Go) { Grow = 1 };
                    side.Rows.Add((heading, card));
                    return [heading, card];
                },
            }),
            (side.Foot = new Label("", UiType.Caption) { Role = TextRole.Dim }),
        },
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

    private Column Compare() => _compareView = new Column
    {
        Grow = 1,
        Cross = StackCrossAlignment.Stretch,
        Gap = Space.Sm,
        Hidden = true,
        Children =
        {
            (_compareTitle = new Label("", UiType.Label) { Align = TextAlign.Center, Stretch = true, Fit = true }),
            new Row
            {
                Grow = 1,
                Cross = StackCrossAlignment.Stretch,
                Gap = Space.Xl,
                Children =
                {
                    (_compareLines = new Column { Free = true }),
                    ComparePart(_onlyA, AtlasCommand.Go, _onlyAHead = Heading()),
                    new Column
                    {
                        Width = CompareCardWidth,
                        Stretch = true,
                        Main = StackMainAlignment.Center,
                        Gap = Space.Md,
                        Children =
                        {
                            (_aCard = new BigCard(CompareCardWidth)),
                            new Button("SWAP")
                            {
                                Tone = Tone.Secondary,
                                Stretch = true,
                                Command = UiCommand.Of(AtlasCommand.Swap),
                                Tooltip = "swap the two",
                            },
                        },
                    },
                    ComparePart(_shared, AtlasCommand.Go, new Label("BOTH", UiType.Small) { Role = TextRole.Accent }),
                    new Column
                    {
                        Width = CompareCardWidth,
                        Stretch = true,
                        Main = StackMainAlignment.Center,
                        Children = { (_bCard = new BigCard(CompareCardWidth)) },
                    },
                    ComparePart(_onlyB, AtlasCommand.GoSecond, _onlyBHead = Heading()),
                },
            },
        },
    };

    private static Label Heading() => new("", UiType.Small) { Role = TextRole.Dim, Fit = true };

    private Column ComparePart(CompareColumn column, AtlasCommand press, Label heading) => new()
    {
        Grow = 1,
        Cross = StackCrossAlignment.Stretch,
        Gap = Space.Xs,
        Children =
        {
            heading,
            (column.List = new PooledList(CardHeight, CardGap)
            {
                Grow = 1,
                Changed = () => FillCompare(column),
                Row = _ => [Remember(column.Rows, new TypeCard(press) { Grow = 1 })],
            }),
            (column.Foot = new Label("", UiType.Caption) { Role = TextRole.Dim }),
        },
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

    private Column Similar() => _similarView = new Column
    {
        Grow = 1,
        Cross = StackCrossAlignment.Stretch,
        Gap = Space.Sm,
        Hidden = true,
        Children =
        {
            (_similarTitle = new Label("", UiType.Label) { Stretch = true, Fit = true }),
            (_similarList = new PooledList(CardHeight, CardGap)
            {
                Grow = 1,
                Changed = FillSimilar,
                Row = _ =>
                {
                    var row = new SimilarRow(
                        new TypeCard(AtlasCommand.Pair) { Width = SimilarCardWidth },
                        new TypeCard(AtlasCommand.Pair) { Width = SimilarCardWidth },
                        new Label("", UiType.Body) { Width = ScoreWidth },
                        new Label("", UiType.Small) { Role = TextRole.Dim, Width = BreakdownWidth, Fit = true },
                        new Label("", UiType.Small) { Role = TextRole.Dim, Grow = 1, Fit = true });
                    _similarRows.Add(row);
                    return [row.A, row.B, row.Score, row.Breakdown, row.Fields];
                },
            }),
            (_similarFoot = new Label("", UiType.Caption) { Role = TextRole.Dim }),
        },
    };

    private Column Empty() => _empty = new Column
    {
        Grow = 1,
        Main = StackMainAlignment.Center,
        Children = { (_emptyText = new Label("", UiType.Section) { Role = TextRole.Dim, Align = TextAlign.Center, Stretch = true }) },
    };

    private static T Remember<T>(List<T> into, T item)
    {
        into.Add(item);
        return item;
    }
}
