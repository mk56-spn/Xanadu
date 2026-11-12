using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host.Tools;
using Xanadu.Systems.Input;
using Xanadu.Tools.DevTools;

namespace Xanadu.Tools.Atlas;

/// <summary>
/// Atlas: the project's types as a chart to click through (<see cref="CodeMap"/>).
/// The list on the left finds a type by typing its name. FOCUS shows one type in
/// the middle, what points at it on the left and what it points at on the
/// right, each group by how (queries, implements, inherits, uses), with lines
/// coloured the same way. COMPARE puts two types side by side: what only the
/// first touches, what both touch in the middle, what only the second touches,
/// so "do these share code?" is one look. SIMILAR ranks types by how
/// alike they are (<see cref="Similarity"/>), among the kinds the top bar shows:
/// the most alike pairs overall, or with a type picked, the ones most like it;
/// click one to compare. Click any card to go there; BACK goes back. Kinds can
/// be hidden to cut noise (enums, plain structs) from the chart; the list always
/// has every kind. SORT orders the list and every chart column the same way: by
/// name, kind, project, or how many types use it or it uses; clicking the chosen
/// sort again flips it (least used first).
/// </summary>
/// <remarks>
/// It is ECS UI built into its tab's body (<see cref="AtlasStage.Build"/>): the
/// columns are pooled lists, the lines connectors, and the colours the theme's
/// categories. Presses arrive as <see cref="AtlasCommand"/>s through the store's
/// tag events; each one changes the state and the next <see cref="Update"/>
/// shows it.
/// </remarks>
internal sealed partial class AtlasStage : IToolStage
{
    /// <summary>How the list and the chart's columns are ordered; ties go by name.</summary>
    internal enum SortMode
    {
        /// <summary>A to Z; while finding, names starting with the search first, then shorter ones.</summary>
        Name,

        /// <summary>Most used first: how many types point at it.</summary>
        Used,

        /// <summary>Uses most first: how many types it points at.</summary>
        Uses,

        /// <summary>By kind, in the kind chips' order.</summary>
        Type,

        /// <summary>By assembly.</summary>
        Project,
    }

    /// <summary>What the chart shows.</summary>
    internal enum View
    {
        Focus,
        Compare,
        Similar,
    }

    /// <summary>What Atlas's buttons and cards do; the argument is a view, kind, sort, type id or match.</summary>
    internal enum AtlasCommand
    {
        View,
        Back,
        Kind,
        Sort,

        /// <summary>A card in the list: in COMPARE with a first type, it picks the second.</summary>
        Pick,

        /// <summary>A card in the chart: go to that type.</summary>
        Go,

        /// <summary>A card on COMPARE's right: make it the second type.</summary>
        GoSecond,

        /// <summary>A SIMILAR row: compare its two types.</summary>
        Pair,

        Swap,
    }

    private static readonly (SortMode Mode, string Label, string Hint)[] Sorts =
    [
        (SortMode.Name, "NAME", "A to Z; while finding, names that start with the search come first"),
        (SortMode.Used, "USED", "most used first: how many types point at it"),
        (SortMode.Uses, "USES", "biggest users first: how many types it points at"),
        (SortMode.Type, "TYPE", "by kind: components, tags, systems, groups, interfaces, structs, classes, enums"),
        (SortMode.Project, "PROJECT", "by the assembly it's built into"),
    ];

    private readonly DevToolsContext _context;
    private readonly CodeMap _map;
    private readonly (string Assembly, int Components, int Tags) _heaviest;
    private readonly Similarity _similarity;
    private readonly HashSet<NodeKind> _hidden = [NodeKind.Enum];
    private readonly Stack<(int Focus, int Other)> _back = new();
    private IDisposable? _presses;
    private string _query = "";
    private int _focus = -1;
    private SortMode _sort;

    /// <summary>The sort flipped: Z to A, least used first, and so on.</summary>
    private bool _reverse;

    /// <summary>The second type while comparing; -1 in FOCUS.</summary>
    private int _other = -1;

    private View _view;
    private bool Comparing => _view == View.Compare;
    private bool _escapeWasDown;

    /// <summary>A press changed what is shown; <see cref="Update"/> shows it, its chart columns back at the top.</summary>
    private bool _changed = true;

    public AtlasStage(DevToolsContext context, CodeMap map, AtlasOptions options)
    {
        (string? focus, string? compare, _sort, _reverse, bool similar) = options;
        _context = context;
        _map = map;
        _heaviest = _map.Heaviest();
        _similarity = new Similarity(_map);

        if (compare?.Split(',') is [var a, var b] && _map.Find(a.Trim()) is { } first && _map.Find(b.Trim()) is { } second)
        {
            _view = View.Compare;
            _focus = first.Id;
            _other = second.Id;
        }
        else if (focus is not null && _map.Find(focus) is { } node)
            _focus = node.Id;
        if (similar)
            _view = View.Similar;
    }

    public void Build(UiNode body)
    {
        BuildPage(body);
        _presses = UiCommandEvents.OnPressed<AtlasCommand>(body.Entity.Store, Pressed);
    }

    public void Update(in ToolFrame frame)
    {
        bool escape = _context.Keyboard.IsPressed(Key.Escape);
        if (escape && !_escapeWasDown)
            _search.Node.Entity.GetComponent<TextField>().Value = "";
        _escapeWasDown = escape;

        // Typing always finds: the search keeps the keys whatever was clicked last.
        if (!_search.Node.Entity.Tags.Has<TextFieldFocused>())
            _search.Node.Entity.AddTag<TextFieldFocused>();

        string typed = _search.Node.Entity.GetComponent<TextField>().Value;
        if (typed != _query)
        {
            _query = typed;
            ShowList(toTop: true);
        }

        if (!_changed)
            return;

        _changed = false;
        Show();
    }

    // ---- presses --------------------------------------------------------------------

    private void Pressed(AtlasCommand command, UiCommand press, Entity _)
    {
        int arg = press.Arg;
        switch (command)
        {
            case AtlasCommand.View:
                _view = (View)arg;
                break;
            case AtlasCommand.Back:
                if (_back.Count > 0)
                    (_focus, _other) = _back.Pop();
                break;
            case AtlasCommand.Kind:
                if (!_hidden.Remove((NodeKind)arg))
                    _hidden.Add((NodeKind)arg);
                break;
            case AtlasCommand.Sort:
                _reverse = _sort == (SortMode)arg && !_reverse;
                _sort = (SortMode)arg;
                break;
            case AtlasCommand.Pick:
                Go(arg, second: Comparing && _focus >= 0);
                break;
            case AtlasCommand.Go:
                Go(arg);
                break;
            case AtlasCommand.GoSecond:
                Go(arg, second: true);
                break;
            case AtlasCommand.Pair when arg < _matches.Count:
                Open(_matches[arg].A, _matches[arg].B);
                break;
            case AtlasCommand.Swap:
                (_focus, _other) = (_other, _focus);
                break;
            default:
                return;
        }

        _changed = true;
    }

    /// <summary>Goes to a type: in COMPARE the right-hand side sets the second, otherwise the first.</summary>
    private void Go(int id, bool second = false)
    {
        if (id == (second ? _other : _focus))
            return;
        _back.Push((_focus, _other));
        if (second)
            _other = id;
        else
            _focus = id;
    }

    /// <summary>Opens COMPARE on two types.</summary>
    private void Open(int a, int b)
    {
        _back.Push((_focus, _other));
        (_focus, _other) = (a, b);
        _view = View.Compare;
    }

    /// <summary>
    /// Goes to the type named <paramref name="name"/> in FOCUS, as clicking it
    /// would (BACK returns from it); false when the chart has no such type.
    /// </summary>
    public bool Show(string name)
    {
        if (_map.Find(name) is not { } node)
            return false;

        _view = View.Focus;
        _other = -1;
        Go(node.Id);
        _changed = true;
        return true;
    }

    // ---- ordering -------------------------------------------------------------------

    /// <summary>Distinct types on one side of a node, whatever the link.</summary>
    private static int Degree(Dictionary<LinkKind, SortedSet<int>> links) => links.Values.SelectMany(set => set).Distinct().Count();

    /// <summary>Ids in the chosen sort, ties by name.</summary>
    private List<int> Sorted(IEnumerable<int> ids)
    {
        IEnumerable<CodeNode> nodes = ids.Select(id => _map.Nodes[id]);
        bool flip = _reverse;
        IOrderedEnumerable<CodeNode> ordered = _sort switch
        {
            SortMode.Used => By(nodes, n => Degree(n.In), !flip),
            SortMode.Uses => By(nodes, n => Degree(n.Out), !flip),
            SortMode.Type => By(nodes, n => n.Kind, flip),
            SortMode.Project => By(nodes, n => n.Assembly, flip, StringComparer.Ordinal),
            // Finding by name: what starts with the search, then the shortest, is what's being typed.
            _ when _query.Length > 0 => nodes
                .OrderBy(n => n.Name.StartsWith(_query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(n => n.Name.Length),
            _ => nodes.OrderBy(_ => 0),
        };
        // Ties go A to Z, unless it's the name being flipped.
        return (flip && _sort == SortMode.Name
                ? ordered.ThenByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
            .Select(n => n.Id).ToList();
    }

    private static IOrderedEnumerable<CodeNode> By<T>(IEnumerable<CodeNode> nodes, Func<CodeNode, T> key, bool descending, IComparer<T>? comparer = null) =>
        descending ? nodes.OrderByDescending(key, comparer) : nodes.OrderBy(key, comparer);

    private IEnumerable<int> Visible(IEnumerable<int> ids) => ids.Distinct().Where(id => !_hidden.Contains(_map.Nodes[id].Kind));

    private static string Heading(LinkKind kind, bool incoming) => (kind, incoming) switch
    {
        (LinkKind.Queries, true) => "QUERIED BY",
        (LinkKind.Implements, true) => "IMPLEMENTED BY",
        (LinkKind.Inherits, true) => "INHERITED BY",
        (LinkKind.Uses, true) => "USED BY",
        (LinkKind.Queries, false) => "QUERIES",
        (LinkKind.Implements, false) => "IMPLEMENTS",
        (LinkKind.Inherits, false) => "INHERITS",
        _ => "USES",
    };

    /// <summary>How one type touches another, either way round; null when it doesn't.</summary>
    private static LinkKind? Linked(CodeNode a, CodeNode b)
    {
        foreach (var (kind, ids) in a.Out)
        {
            if (ids.Contains(b.Id))
                return kind;
        }

        foreach (var (kind, ids) in a.In)
        {
            if (ids.Contains(b.Id))
                return kind;
        }

        return null;
    }

    private static string Direct(CodeNode a, CodeNode b)
    {
        if (a.Out.Any(pair => pair.Value.Contains(b.Id)))
            return $"; {a.Name} {Heading(a.Out.First(pair => pair.Value.Contains(b.Id)).Key, false).ToLowerInvariant()} {b.Name}";
        if (b.Out.Any(pair => pair.Value.Contains(a.Id)))
            return $"; {b.Name} {Heading(b.Out.First(pair => pair.Value.Contains(a.Id)).Key, false).ToLowerInvariant()} {a.Name}";
        return "; neither touches the other";
    }

    /// <summary>A type's field types, short: <c>float, Vector2, List&lt;Entity&gt;</c>.</summary>
    private static string Fields(CodeNode node) =>
        node.Fields.Count == 0 ? "no fields" : string.Join(", ", node.Fields.Select(f => TypeNames.Short(f.Type)));

    /// <summary>Typing goes back to the rest of the UI once the tab closes.</summary>
    public void Hidden() => _search.Node.Entity.RemoveTag<TextFieldFocused>();

    public void Dispose()
    {
        _presses?.Dispose();
        _presses = null;
    }
}

/// <summary>How Atlas opens: on a type, comparing two, sorted a way, or on SIMILAR.</summary>
internal readonly record struct AtlasOptions(
    string? Focus = null,
    string? Compare = null,
    AtlasStage.SortMode Sort = AtlasStage.SortMode.Name,
    bool Reverse = false,
    bool Similar = false);
