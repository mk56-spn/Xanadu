using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Atlas;

/// <summary>Showing the state: the header's choices, the list, and whichever chart view is open, each list filled as it scrolls.</summary>
internal sealed partial class AtlasStage
{
    private List<int> _listIds = new();
    private readonly List<(int Id, LinkKind Kind)> _inRows = new(), _outRows = new();
    private List<int> _onlyAIds = new(), _sharedIds = new(), _onlyBIds = new();
    private List<Match> _matches = new();

    /// <summary>Everything, after a press: the chart's columns go back to the top.</summary>
    private void Show()
    {
        ShowHeader();
        ShowList(toTop: false);
        ShowChart();
    }

    private void ShowHeader()
    {
        for (int i = 0; i < _views.Length; i++)
            _views[i].Chosen((int)_view == i);

        UiThemeRoles roles = _search.Roles;
        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            (UiNode chip, UiNode swatch, UiNode label) = _kinds[(int)kind];
            bool shown = !_hidden.Contains(kind);
            if (shown)
                swatch.CategoryFill(KindCategory(kind));
            else
                swatch.Fill(roles.Inset);
            label.TextColor(shown ? roles.Text : roles.TextDim);
            chip.Tooltip($"{(shown ? "hide" : "show")} {kind.ToString().ToLowerInvariant()}s in the chart");
        }

        for (int i = 0; i < Sorts.Length; i++)
        {
            (SortMode mode, string label, string hint) = Sorts[i];
            bool on = _sort == mode;
            string name = label.ToLowerInvariant();
            _sorts[i].Chosen(on)
                .SetText(on ? label + (_reverse ? " ▲" : " ▼") : label)
                .Tooltip(on
                    ? $"sorted by {name}{(_reverse ? ", flipped" : "")}: {hint}; click to flip"
                    : $"sort by {name}: {hint}");
        }
    }

    /// <summary>Every type whose name has the search in it, best matches first. The kind filter is the chart's only.</summary>
    private void ShowList(bool toTop)
    {
        _listIds = Sorted(_map.Nodes.Where(n => _query.Length == 0 || n.Name.Contains(_query, StringComparison.OrdinalIgnoreCase)).Select(n => n.Id));
        UiLists.SetCount(_list, _listIds.Count, keepPlace: !toTop);
    }

    private void ShowChart()
    {
        CodeNode? focus = _focus >= 0 ? _map.Nodes[_focus] : null;
        CodeNode? other = _other >= 0 ? _map.Nodes[_other] : null;
        bool pair = focus is not null && other is not null && focus != other;
        View? open = _view switch
        {
            View.Focus when focus is not null => View.Focus,
            View.Compare when pair => View.Compare,
            View.Similar => View.Similar,
            _ => null,
        };

        UiLayout.SetHidden(_focusView, open != View.Focus);
        UiLayout.SetHidden(_compareView, open != View.Compare);
        UiLayout.SetHidden(_similarView, open != View.Similar);
        UiLayout.SetHidden(_empty, open is not null);
        _emptyText.SetText(focus is null || _view == View.Focus
            ? "pick a type on the left"
            : $"pick a type on the left to compare with {focus.Name}");

        switch (open)
        {
            case View.Focus:
                ShowFocus(focus!);
                break;
            case View.Compare:
                ShowCompare(focus!, other!);
                break;
            case View.Similar:
                ShowSimilar(focus);
                break;
        }
    }

    private void ShowFocus(CodeNode node)
    {
        ShowBigCard(_centre, node);
        Rows(node.In, _inRows);
        Rows(node.Out, _outRows);
        ShowSide(_in, _inRows, "nothing points here");
        ShowSide(_out, _outRows, "points at nothing");
    }

    /// <summary>One side's links, grouped by kind, each group under a heading row (a negative id).</summary>
    private void Rows(Dictionary<LinkKind, SortedSet<int>> links, List<(int Id, LinkKind Kind)> rows)
    {
        rows.Clear();
        foreach (LinkKind kind in Enum.GetValues<LinkKind>())
        {
            if (!links.TryGetValue(kind, out var ids))
                continue;
            List<int> shown = Sorted(Visible(ids));
            if (shown.Count == 0)
                continue;
            rows.Add((-1, kind));
            rows.AddRange(shown.Select(id => (id, kind)));
        }
    }

    private static void ShowSide(FocusSide side, List<(int Id, LinkKind Kind)> rows, string none)
    {
        UiLayout.SetHidden(side.Empty, rows.Count > 0);
        side.Empty.SetText(none);
        UiLists.SetCount(side.List, rows.Count);
    }

    private void ShowCompare(CodeNode a, CodeNode b)
    {
        HashSet<int> Near(CodeNode n) => Visible(n.In.Values.Concat(n.Out.Values).SelectMany(set => set)).Where(id => id != a.Id && id != b.Id).ToHashSet();
        HashSet<int> aNear = Near(a), bNear = Near(b);
        _sharedIds = Sorted(aNear.Intersect(bNear));
        _onlyAIds = Sorted(aNear.Except(bNear));
        _onlyBIds = Sorted(bNear.Except(aNear));

        _compareTitle.SetText($"{a.Name} touches {aNear.Count}, {b.Name} touches {bNear.Count}; they share {_sharedIds.Count}" + Direct(a, b));
        _onlyAHead.SetText($"ONLY {a.Name.ToUpperInvariant()}");
        _onlyBHead.SetText($"ONLY {b.Name.ToUpperInvariant()}");
        ShowBigCard(_aCard, a);
        ShowBigCard(_bCard, b);
        LinkKind? direct = Linked(a, b);
        UiConnectors.Join(_directLine, direct is null ? default : _aCard.Box, _bCard.Box, LinkCategory(direct ?? LinkKind.Uses));

        UiLists.SetCount(_onlyA.List, _onlyAIds.Count);
        UiLists.SetCount(_shared.List, _sharedIds.Count);
        UiLists.SetCount(_onlyB.List, _onlyBIds.Count);
    }

    /// <summary>With a type picked, the types most like it; otherwise the most alike pairs of all.</summary>
    private void ShowSimilar(CodeNode? picked)
    {
        // The pool is whatever kinds the top bar shows: components alone, or every kind against every other.
        var pool = Enum.GetValues<NodeKind>().Where(kind => !_hidden.Contains(kind)).ToHashSet();
        _matches = picked is null ? _similarity.Pairs(pool) : _similarity.Like(picked.Id, pool);
        string kinds = pool.Count == 1 ? pool.First().ToString().ToLowerInvariant() + "s" : "shown kinds";
        string title = _matches.Count == 0
            ? "nothing shares a field type or a user with it"
            : picked is null
                ? $"the {_matches.Count} most alike pairs of {kinds}: same field types (shape), same users, same uses; pick a type on the left for its own"
                : $"{kinds} like {picked.Name} ({Fields(picked)}): same field types (shape), same users, same uses";
        _similarTitle.SetText(title);
        UiLists.SetCount(_similarList, _matches.Count);
    }

    // ---- filling the lists' rows ----------------------------------------------------

    private void FillList()
    {
        UiList view = _list.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int id = _listIds[view.First + i];
            (UiNode used, Card card) = _listRows[i];
            used.SetText(Degree(_map.Nodes[id].In).ToString());
            ShowCard(card, id, selected: id == _focus || id == _other);
        }

        Foot(_listFoot, view);
    }

    private void FillSide(FocusSide side, bool incoming)
    {
        List<(int Id, LinkKind Kind)> rows = incoming ? _inRows : _outRows;
        UiList view = side.List.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            (int id, LinkKind kind) = rows[view.First + i];
            (UiNode heading, Card card) = side.Rows[i];
            UiLayout.SetHidden(heading, id >= 0);
            UiLayout.SetHidden(card.Box, id < 0);
            if (id < 0)
            {
                heading.SetText(Heading(kind, incoming)).CategoryText(LinkCategory(kind));
                continue;
            }

            ShowCard(card, id);
            UiConnectors.Join(side.Lines[i], card.Box, _centre.Box, LinkCategory(kind));
        }

        Foot(side.Foot, view);
    }

    private void FillCompare(CompareColumn column)
    {
        List<int> ids = column == _onlyA ? _onlyAIds : column == _shared ? _sharedIds : _onlyBIds;
        UiList view = column.List.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int id = ids[view.First + i];
            Card card = column.Rows[i];
            ShowCard(card, id);
            CodeNode node = _map.Nodes[id];
            if (column.ToA.Count > 0)
                UiConnectors.Join(column.ToA[i], card.Box, _aCard.Box, Touches(_map.Nodes[_focus], node));
            if (column.ToB.Count > 0)
                UiConnectors.Join(column.ToB[i], card.Box, _bCard.Box, Touches(_map.Nodes[_other], node));
        }

        Foot(column.Foot, view);
    }

    private void FillSimilar()
    {
        bool picked = _focus >= 0;
        UiList view = _similarList.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown; i++)
        {
            int index = view.First + i;
            Match match = _matches[index];
            SimilarRow row = _similarRows[i];
            UiLayout.SetHidden(row.A.Box, picked);
            row.B.Box.Width(picked ? SimilarPickedWidth : SimilarCardWidth);
            ShowCard(row.A, match.A, arg: index);
            ShowCard(row.B, match.B, arg: index);
            string compare = $"click to compare {_map.Nodes[match.A].Name} with {_map.Nodes[match.B].Name}";
            row.A.Box.Tooltip($"{_map.Nodes[match.A].FullName}  ·  {Fields(_map.Nodes[match.A])}  ·  {compare}");
            row.B.Box.Tooltip($"{_map.Nodes[match.B].FullName}  ·  {Fields(_map.Nodes[match.B])}  ·  {compare}");

            UiThemeRoles roles = row.Score.Roles;
            row.Score.SetText($"{match.Score * 100:0}%").TextColor(roles.TextDim.Lerp(roles.Accent, match.Score));
            row.Breakdown.SetText($"shape {Match.Percent(match.Shape)}  ·  users {Match.Percent(match.Users)}  ·  uses {Match.Percent(match.Uses)}");
            string fields = picked ? Fields(_map.Nodes[match.B]) : $"{Fields(_map.Nodes[match.A])}  |  {Fields(_map.Nodes[match.B])}";
            row.Fields.SetText(fields);
        }

        Foot(_similarFoot, view);
    }

    // ---- cards ----------------------------------------------------------------------

    /// <summary>Shows type <paramref name="id"/> on a small card, which then presses with it (or with <paramref name="arg"/>).</summary>
    private void ShowCard(Card card, int id, bool selected = false, int arg = -1)
    {
        CodeNode node = _map.Nodes[id];
        int category = KindCategory(node.Kind);
        card.Box.Entity.GetComponent<UiCommand>().Arg = arg >= 0 ? arg : id;
        card.Box.CategoryTone(category)
            .Border(selected ? card.Box.Roles.Chosen.Fill : default, UiMetrics.Border.Medium)
            .Tooltip($"{node.FullName}  ·  {node.Kind.ToString().ToLowerInvariant()}  ·  {node.Assembly}  ·  uses {Degree(node.Out)}, used by {Degree(node.In)}  ·  click to go there");
        card.Stripe.CategoryFill(category);
        card.Name.SetText(node.Name);
    }

    private static void ShowBigCard(BigCard card, CodeNode node)
    {
        int category = KindCategory(node.Kind);
        card.Box.CategoryTone(category).Border(card.Box.Roles.Category(category), UiMetrics.Border.Medium).Tooltip(node.FullName);
        card.Name.SetText(node.Name);
        card.Kind.SetText(node.Kind.ToString().ToLowerInvariant() + "  ·  " + node.Assembly.Replace("Xanadu.", "")).CategoryText(category);
        card.Space.SetText(node.Namespace);
    }

    /// <summary>Says which rows of how many a list shows, when it can't show them all.</summary>
    private static void Foot(UiNode foot, UiList view)
    {
        bool all = view.First == 0 && view.Shown >= view.Count;
        foot.SetText(all ? "" : $"{view.First + 1}–{view.First + view.Shown} of {view.Count} · wheel to scroll");
    }

    /// <summary>The colour of how <paramref name="centre"/> touches <paramref name="node"/>, as its line shows it.</summary>
    private static int Touches(CodeNode centre, CodeNode node) => LinkCategory(Linked(centre, node) ?? LinkKind.Uses);

    /// <summary>Each kind's colour among the theme's categories.</summary>
    private static int KindCategory(NodeKind kind) => kind switch
    {
        NodeKind.Component => 0,
        NodeKind.Tag => 1,
        NodeKind.System => 2,
        NodeKind.Group => 3,
        NodeKind.Interface => 4,
        NodeKind.Struct => 5,
        NodeKind.Class => 7,
        _ => 8,
    };

    /// <summary>Each link's colour among the theme's categories: queries share the components' gold, implements the interfaces' violet.</summary>
    private static int LinkCategory(LinkKind kind) => kind switch
    {
        LinkKind.Queries => 0,
        LinkKind.Implements => 4,
        LinkKind.Inherits => 6,
        _ => 7,
    };
}
