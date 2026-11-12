using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Atlas;

/// <summary>Atlas's tree, built once: the header, the list, and the three chart views, each column a pooled list.</summary>
internal sealed partial class AtlasStage
{
    private const float ListWidth = 420;

    /// <summary>The margin left of each list card, where how many types use it goes.</summary>
    private const float UsedWidth = 40;

    private const float CardHeight = UiMetrics.Control.Xs;
    private const float CardGap = UiMetrics.Space.Xs;
    private const float StripeWidth = UiMetrics.Space.Xs;
    private const float SwatchSize = UiMetrics.Space.Md;
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

    /// <summary>A small card: the clickable box tinted by kind, its kind's stripe, and the name.</summary>
    private readonly record struct Card(UiNode Box, UiNode Stripe, UiNode Name);

    /// <summary>The card a chart centres on: name, kind and project, namespace.</summary>
    private readonly record struct BigCard(UiNode Box, UiNode Name, UiNode Kind, UiNode Space);

    // Header.
    private readonly UiNode[] _views = new UiNode[3];
    private readonly List<(UiNode Chip, UiNode Swatch, UiNode Label)> _kinds = new();
    private readonly UiNode[] _sorts = new UiNode[Sorts.Length];
    private UiNode _search;

    // The list.
    private UiNode _list, _listFoot;
    private readonly List<(UiNode Used, Card Card)> _listRows = new();

    // The chart's views, and what it says when it has nothing to show.
    private UiNode _focusView, _compareView, _similarView, _empty, _emptyText;

    // FOCUS: a side each, of headings and cards, with a line per row to the centre.
    private readonly FocusSide _in = new(), _out = new();
    private BigCard _centre;

    // COMPARE.
    private UiNode _compareTitle, _onlyAHead, _onlyBHead;
    private readonly CompareColumn _onlyA = new(), _shared = new(), _onlyB = new();
    private BigCard _aCard, _bCard;
    private UiNode _directLine;

    // SIMILAR.
    private UiNode _similarTitle, _similarList, _similarFoot;
    private readonly List<SimilarRow> _similarRows = new();

    private sealed class FocusSide
    {
        public UiNode List, Foot, Empty;
        public readonly List<(UiNode Heading, Card Card)> Rows = new();
        public readonly List<UiNode> Lines = new();
    }

    private sealed class CompareColumn
    {
        public UiNode List, Foot;
        public readonly List<Card> Rows = new();

        /// <summary>A line per row to the first card, and one to the second (only the shared column uses both).</summary>
        public readonly List<UiNode> ToA = new(), ToB = new();
    }

    private readonly record struct SimilarRow(Card A, Card B, UiNode Score, UiNode Breakdown, UiNode Fields);

    private void BuildPage(UiNode body)
    {
        UiNode page = body.Column().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Md);
        BuildHeader(page.Row().Gap(UiMetrics.Space.Sm));
        BuildBar(page.Row().Gap(UiMetrics.Space.Sm));

        UiNode main = page.Row().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xl);
        BuildList(main.Column().Width(ListWidth).Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xs));

        UiNode chart = main.Column().Grow().Cross(StackCrossAlignment.Stretch);
        BuildFocus(_focusView = chart.Row().Grow().Cross(StackCrossAlignment.Stretch));
        BuildCompare(_compareView = chart.Column().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Sm));
        BuildSimilar(_similarView = chart.Column().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Sm));
        _empty = chart.Column().Grow().Main(StackMainAlignment.Center);
        _emptyText = _empty.Text("", UiType.Section).DimText().Align(TextAlign.Center).Stretch();
    }

    private void BuildHeader(UiNode header)
    {
        header.Text("ATLAS", UiType.Heading).AccentText();
        _views[(int)View.Focus] = ViewButton(header, View.Focus, "FOCUS", "one type: what points at it, and what it points at");
        _views[(int)View.Compare] = ViewButton(header, View.Compare, "COMPARE",
            "two types side by side: what each touches alone, and what both touch; the list then picks the second");
        _views[(int)View.Similar] = ViewButton(header, View.Similar, "SIMILAR",
            "types that could be one: same field types, same users and same uses, among the kinds the top bar shows; pick a type to see the ones like it");
        header.Button("BACK", UiType.Label).Secondary().With(UiCommand.Of(AtlasCommand.Back)).Tooltip("back to the last type");

        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            UiNode chip = header.Row().Gap(UiMetrics.Space.Sm).Pad(UiMetrics.Space.Sm).Input()
                .With(ButtonVisual.Create(default)).Secondary()
                .With(UiCommand.Of(AtlasCommand.Kind, (int)kind));
            UiNode swatch = chip.Rect(default, size: new Vector2(SwatchSize, SwatchSize));
            UiNode label = chip.Text(kind.ToString().ToUpperInvariant(), UiType.Small);
            _kinds.Add((chip, swatch, label));
        }

        header.Column().Grow();
        UiNode budget = header.Column().Gap(UiMetrics.Space.Xs);
        (string assembly, int components, int tags) = _heaviest;
        string exe = assembly.Replace("Xanadu.", "");
        Budget(budget, "COMPONENTS", components, CodeMap.MaxComponents, exe, "component types; one more than the limit makes CreateEntity throw");
        Budget(budget, "TAGS", tags, CodeMap.MaxTags, exe, "tag types; past the limit tags silently share bits and queries go wrong");
    }

    private static UiNode ViewButton(UiNode header, View view, string label, string hint) =>
        header.Button(label, UiType.Label).With(UiCommand.Of(AtlasCommand.View, (int)view)).Tooltip(hint);

    /// <summary>
    /// One of the ECS's hard limits: types of one sort in the heaviest exe against
    /// Friflo's 255. Amber past three quarters, red past nine tenths.
    /// </summary>
    private static void Budget(UiNode parent, string label, int count, int max, string exe, string why)
    {
        UiNode row = parent.Row().Gap(UiMetrics.Space.Sm).Input()
            .Tooltip($"{count} {why}. Counted for {exe} with what it references, the heaviest exe, Friflo's own included; the limit is per process.");
        row.Text(label, UiType.Small).DimText().Width(BudgetLabelWidth);
        row.Meter((float)count / max).Size(MeterWidth, UiMetrics.Space.Sm);
        row.Text($"{count} / {max}", UiType.Small).Width(BudgetCountWidth);
    }

    private void BuildBar(UiNode bar)
    {
        // A text field: the field system gives it its fill, focused or not.
        _search = bar.Rect(default, "", new Vector2(ListWidth, UiMetrics.Control.Sm))
            .Input()
            .With(new TextStyle(UiType.Label, bar.Roles.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .Tooltip("type to find a type; Escape clears it");

        bar.Text("SORT", UiType.Small).DimText();
        for (int i = 0; i < Sorts.Length; i++)
        {
            // Built with the arrow the chosen sort shows, so the measured box fits it.
            _sorts[i] = bar.Button(Sorts[i].Label + " ▼", UiType.Small)
                .With(UiCommand.Of(AtlasCommand.Sort, (int)Sorts[i].Mode));
        }

        string built = string.Join("  ·  ", _map.Assemblies.Select(a => $"{a.Name.Replace("Xanadu.", "")} {a.Built:HH:mm}"));
        UiNode note = bar.Text("", UiType.Small).DimText().Grow().Fit();
        note.Tooltip(built);
        note.SetText($"{_map.Nodes.Count} types from the last builds: {built}");
    }

    private void BuildList(UiNode column)
    {
        _list = column.List(CardHeight, CardGap, row =>
        {
            UiNode used = row.Text("", UiType.Small).DimText().Align(TextAlign.Right).Width(UsedWidth).Stretch();
            _listRows.Add((used, BuildCard(row, AtlasCommand.Pick)));
        }).Grow();
        _listFoot = column.Text("", UiType.Caption).DimText();
        _list.Entity.OnChanged(FillList);
    }

    private void BuildFocus(UiNode view)
    {
        // The lines go first, so every card draws over their ends.
        UiNode lines = view.Column().IgnoreLayout();
        // Fixed sides with the room shared either side of the centre, so the lines fan out across it.
        BuildSide(view, _in);
        view.Column().Grow();
        UiNode centre = view.Column().Width(BigCardWidth).Stretch().Main(StackMainAlignment.Center);
        _centre = BuildBigCard(centre, BigCardWidth);
        view.Column().Grow();
        BuildSide(view, _out);

        foreach (FocusSide side in new[] { _in, _out })
        {
            foreach ((_, Card card) in side.Rows)
                side.Lines.Add(lines.Connector(card.Box, _centre.Box, 0));
        }
    }

    private void BuildSide(UiNode view, FocusSide side)
    {
        UiNode column = view.Column().Width(SideWidth).Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xs);
        side.Empty = column.Text("", UiType.Body).DimText().Align(TextAlign.Center).Stretch();
        side.List = column.List(CardHeight, CardGap, row =>
        {
            UiNode heading = row.Text("", UiType.Small).Grow().Stretch();
            side.Rows.Add((heading, BuildCard(row, AtlasCommand.Go)));
        }).Grow();
        side.Foot = column.Text("", UiType.Caption).DimText();
        side.List.Entity.OnChanged(() => FillSide(side, side == _in));
    }

    private void BuildCompare(UiNode view)
    {
        _compareTitle = view.Text("", UiType.Label).Align(TextAlign.Center).Stretch().Fit();
        UiNode row = view.Row().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xl);
        UiNode lines = row.Column().IgnoreLayout();

        _onlyAHead = BuildCompareColumn(row, _onlyA, AtlasCommand.Go);
        UiNode aSide = row.Column().Width(CompareCardWidth).Stretch().Main(StackMainAlignment.Center).Gap(UiMetrics.Space.Md);
        _aCard = BuildBigCard(aSide, CompareCardWidth);
        aSide.Button("SWAP", UiType.Label).Secondary().Stretch().With(UiCommand.Of(AtlasCommand.Swap)).Tooltip("swap the two");
        BuildCompareColumn(row, _shared, AtlasCommand.Go).SetText("BOTH").AccentText();
        _bCard = BuildBigCard(row.Column().Width(CompareCardWidth).Stretch().Main(StackMainAlignment.Center), CompareCardWidth);
        _onlyBHead = BuildCompareColumn(row, _onlyB, AtlasCommand.GoSecond);

        _directLine = lines.Connector(_aCard.Box, _bCard.Box, 0);
        foreach (Card card in _onlyA.Rows)
            _onlyA.ToA.Add(lines.Connector(card.Box, _aCard.Box, 0));
        foreach (Card card in _shared.Rows)
        {
            _shared.ToA.Add(lines.Connector(card.Box, _aCard.Box, 0));
            _shared.ToB.Add(lines.Connector(card.Box, _bCard.Box, 0));
        }
        foreach (Card card in _onlyB.Rows)
            _onlyB.ToB.Add(lines.Connector(card.Box, _bCard.Box, 0));
    }

    /// <summary>A COMPARE column under its heading, which it returns.</summary>
    private UiNode BuildCompareColumn(UiNode row, CompareColumn column, AtlasCommand press)
    {
        UiNode stack = row.Column().Grow().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Xs);
        UiNode heading = stack.Text("", UiType.Small).DimText().Fit();
        column.List = stack.List(CardHeight, CardGap, cardRow => column.Rows.Add(BuildCard(cardRow, press))).Grow();
        column.Foot = stack.Text("", UiType.Caption).DimText();
        column.List.Entity.OnChanged(() => FillCompare(column));
        return heading;
    }

    private void BuildSimilar(UiNode view)
    {
        _similarTitle = view.Text("", UiType.Label).Stretch().Fit();
        _similarList = view.List(CardHeight, CardGap, row =>
        {
            Card a = BuildCard(row, AtlasCommand.Pair, grow: false);
            Card b = BuildCard(row, AtlasCommand.Pair, grow: false);
            UiNode score = row.Text("", UiType.Body).Width(ScoreWidth);
            UiNode breakdown = row.Text("", UiType.Small).DimText().Width(BreakdownWidth).Fit();
            UiNode fields = row.Text("", UiType.Small).DimText().Grow().Fit();
            _similarRows.Add(new SimilarRow(a, b, score, breakdown, fields));
        }).Grow();
        _similarFoot = view.Text("", UiType.Caption).DimText();
        _similarList.Entity.OnChanged(FillSimilar);
    }

    /// <summary>A small card that presses <paramref name="press"/> with the type (or row) it shows; it grows along its row unless sized.</summary>
    private static Card BuildCard(UiNode row, AtlasCommand press, bool grow = true)
    {
        UiNode box = row.Row().Cross(StackCrossAlignment.Stretch).Gap(UiMetrics.Space.Sm).Input()
            .With(ButtonVisual.Create(default))
            .With(UiCommand.Of(press))
            .Tooltip("");
        if (grow)
            box.Grow();
        else
            box.Width(SimilarCardWidth);
        UiNode stripe = box.Rect(default, size: new Vector2(StripeWidth, CardHeight));
        UiNode name = box.Text("").Grow().Fit();
        return new Card(box, stripe, name);
    }

    private static BigCard BuildBigCard(UiNode parent, float width)
    {
        UiNode box = parent.Column().Width(width).Pad(UiMetrics.Space.Md).Gap(UiMetrics.Space.Xs)
            .Cross(StackCrossAlignment.Stretch).Input().Tooltip("");
        UiNode name = box.Text("", UiType.Section).Fit();
        UiNode kind = box.Text("", UiType.Small).Fit();
        UiNode space = box.Text("", UiType.Caption).DimText().Fit();
        return new BigCard(box, name, kind, space);
    }
}
