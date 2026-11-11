using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Host;
using Xanadu.Tools.DevTools;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Theme;

namespace Xanadu.Tools.Atlas;

/// <summary>
/// Atlas: the project's types as a chart to click through (<see cref="CodeMap"/>).
/// The list on the left finds a type by typing its name. FOCUS shows one type in
/// the middle, what points at it on the left and what it points at on the
/// right, each group by how (queries, implements, inherits, uses), with lines
/// coloured the same way. COMPARE puts two types side by side: what only the
/// first touches, what both touch in the middle, what only the second touches,
/// so "do these share code?" is one look. SIMILAR ranks components by how
/// alike they are (<see cref="Similarity"/>): the most alike pairs overall, or
/// with a component picked, the ones most like it; click one to compare. Click
/// any card to go there; BACK goes back. Kinds can be hidden to cut noise
/// (enums, plain structs). SORT orders the list and every chart column the
/// same way: by name, kind, project, or how many types use it or it uses;
/// clicking the chosen sort again flips it (least used first).
/// </summary>
internal sealed class AtlasStage : CanvasToolStage
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

    private static readonly (SortMode Mode, string Label, string Hint)[] Sorts =
    [
        (SortMode.Name, "NAME", "A to Z; while finding, names that start with the search come first"),
        (SortMode.Used, "USED", "most used first: how many types point at it"),
        (SortMode.Uses, "USES", "biggest users first: how many types it points at"),
        (SortMode.Type, "TYPE", "by kind: components, tags, systems, groups, interfaces, structs, classes, enums"),
        (SortMode.Project, "PROJECT", "by the assembly it's built into"),
    ];

    private const float Margin = 40;
    private const float ListLeft = Margin, ListWidth = 420;
    private const float Top = 150, Bottom = 1020;
    private const float ChartLeft = ListLeft + ListWidth + 40, ChartRight = 1880;
    private const float Card = 26, CardGap = 4;
    private const int TextSize = 16;

    private static readonly Dictionary<NodeKind, Color> KindColours = new()
    {
        [NodeKind.Component] = Palette.Gold,
        [NodeKind.Tag] = Palette.GoldLight,
        [NodeKind.System] = Palette.InfoSoft,
        [NodeKind.Group] = Palette.Cyan,
        [NodeKind.Interface] = Palette.Violet,
        [NodeKind.Struct] = Palette.Lime,
        [NodeKind.Class] = Palette.TextDim,
        [NodeKind.Enum] = Palette.BorderSubtle,
    };

    private static readonly Dictionary<LinkKind, Color> LinkColours = new()
    {
        [LinkKind.Queries] = Palette.Gold,
        [LinkKind.Implements] = Palette.Violet,
        [LinkKind.Inherits] = Palette.Pink,
        [LinkKind.Uses] = new(0.5f, 0.56f, 0.66f),
    };

    private readonly CodeMap _map;
    private readonly (string Assembly, int Components, int Tags) _heaviest;
    private readonly Similarity _similarity;
    private readonly HashSet<NodeKind> _hidden = [NodeKind.Enum];
    private readonly Stack<(int Focus, int Other)> _back = new();
    private readonly KeyRepeat _backspace = new();
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

    /// <summary>How far each scrolling column is scrolled, by its left edge.</summary>
    private readonly Dictionary<float, int> _scroll = new();

    // Pointer, and what this frame's drawing found under it.
    private Vector2 _mouse;
    private bool _click;
    private bool _wasDown;
    private string? _hint;
    private Action? _onClick;
    private float? _wheelColumn;

    public AtlasStage(DevToolsContext context, Handle item, Rect2 area, CodeMap map, AtlasOptions options)
        : base(context, item, area)
    {
        (string? focus, string? compare, _sort, _reverse, bool similar) = options;
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

    protected override void Tick(float delta, float time)
    {
        _mouse = Mouse;
        bool down = LeftDown;
        _click = down && !_wasDown;
        _wasDown = down;
        _hint = null;
        _onClick = null;
        _wheelColumn = null;
        _cardIds.Clear();
        Type(delta);

        Canvas.Clear(Item);
        Canvas.DrawRect(Item, new Rect2(0, 0, Width, Height), Palette.Background);
        DrawHeader();
        DrawList();
        if (_view == View.Compare)
            DrawCompare();
        else if (_view == View.Similar)
            DrawSimilar();
        else
            DrawFocus();

        if (_hint is not null)
            DrawHint(_hint);

        // The wheel scrolls whichever column the pointer is over.
        int wheel = TakeWheel();
        if (wheel != 0 && _wheelColumn is { } column)
            _scroll[column] = Math.Max(0, _scroll.GetValueOrDefault(column) - wheel * 3);

        if (_click)
            _onClick?.Invoke();
    }

    // ---- input ----------------------------------------------------------------------

    private void Type(float delta)
    {
        string typed = TextInput.TakeTyped();
        foreach (char c in typed)
        {
            if (!char.IsControl(c))
                Search(_query + c);
        }

        int steps = _backspace.Update(Keyboard.IsPressed(Key.Backspace) ? 1 : 0, delta);
        for (int i = 0; i < steps && _query.Length > 0; i++)
            Search(_query[..^1]);

        bool escape = Keyboard.IsPressed(Key.Escape);
        if (escape && !_escapeWasDown)
            Search("");
        _escapeWasDown = escape;
    }

    private void Search(string query)
    {
        _query = query;
        _scroll[ListLeft] = 0;
    }

    /// <summary>Goes to a type: in COMPARE a click on the right half's side sets the second, otherwise the first.</summary>
    private void Go(int id, bool second = false)
    {
        if (id == (second ? _other : _focus))
            return;
        _back.Push((_focus, _other));
        if (second)
            _other = id;
        else
            _focus = id;
        _scroll.Clear();
    }

    /// <summary>Opens COMPARE on two types.</summary>
    private void Open(int a, int b)
    {
        _back.Push((_focus, _other));
        (_focus, _other) = (a, b);
        _view = View.Compare;
        _scroll.Clear();
    }

    private void Back()
    {
        if (_back.Count > 0)
            (_focus, _other) = _back.Pop();
        _scroll.Clear();
    }

    // ---- drawing --------------------------------------------------------------------

    private void DrawHeader()
    {
        Label(new Rect2(Margin, 26, 300, 44), "ATLAS", Palette.Accent, 34);
        Button(new Rect2(190, 30, 120, 38), "FOCUS", _view == View.Focus, () => _view = View.Focus, "one type: what points at it, and what it points at");
        Button(new Rect2(318, 30, 140, 38), "COMPARE", Comparing, () => _view = View.Compare,
            "two types side by side: what each touches alone, and what both touch; the list then picks the second");
        Button(new Rect2(466, 30, 130, 38), "SIMILAR", _view == View.Similar, () =>
        {
            _view = View.Similar;
            _scroll.Clear();
        }, "components that could be one: same field types and same users; pick a component to see the ones like it");
        Button(new Rect2(604, 30, 100, 38), "BACK", false, Back, "back to the last type");

        float x = 724;
        foreach (NodeKind kind in Enum.GetValues<NodeKind>())
        {
            string label = kind.ToString().ToUpperInvariant();
            float width = label.Length * 10 + 40;
            var box = new Rect2(x, 34, width, 30);
            bool shown = !_hidden.Contains(kind);
            bool hover = box.HasPoint(_mouse);
            Canvas.DrawRect(Item, box, hover ? Palette.SurfaceAlt : Palette.Surface);
            Canvas.DrawRect(Item, new Rect2(x + 8, 43, 12, 12), shown ? KindColours[kind] : Palette.Muted);
            Label(new Rect2(x + 26, 34, width - 30, 30), label, shown ? Palette.Text : Palette.TextDim, 14);
            if (hover)
            {
                _hint = $"{(shown ? "hide" : "show")} {kind.ToString().ToLowerInvariant()}s in the chart";
                _onClick = () =>
                {
                    if (!_hidden.Remove(kind))
                        _hidden.Add(kind);
                };
            }

            x += width + 6;
        }

        DrawBudget();

        string built = string.Join("  ·  ", _map.Assemblies.Select(a => $"{a.Name.Replace("Xanadu.", "")} {a.Built:HH:mm}"));
        Label(new Rect2(ChartLeft, 80, ChartRight - ChartLeft, 24), $"{_map.Nodes.Count} types from the last builds: {built}", Palette.TextDim, 14);
        Label(new Rect2(Margin, 76, ListWidth, 30), _query.Length > 0 ? $"find: {_query}_" : "type to find a type", _query.Length > 0 ? Palette.Text : Palette.TextDim, 18);
        DrawSorts();
    }

    /// <summary>The sort chips over the list: one is on, and it orders the chart's columns too; clicking it again flips it.</summary>
    private void DrawSorts()
    {
        float x = ListLeft, y = 112;
        Label(new Rect2(x, y, 44, 28), "SORT", Palette.TextMuted, 14);
        x += 44;
        foreach ((SortMode mode, string label, string hint) in Sorts)
        {
            float width = label.Length * 9 + 30;
            SortMode chosen = mode;
            bool on = _sort == mode;
            string tooltip = on
                ? $"sorted by {label.ToLowerInvariant()}{(_reverse ? ", flipped" : "")}: {hint}; click to flip"
                : $"sort by {label.ToLowerInvariant()}: {hint}";
            Button(new Rect2(x, y, width, 28), on ? label + (_reverse ? " ▲" : " ▼") : label, on, () =>
            {
                _reverse = _sort == chosen && !_reverse;
                _sort = chosen;
                _scroll.Clear();
            }, tooltip, 14);
            x += width + 4;
        }
    }

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

    /// <summary>
    /// The ECS's hard limits, top right: component and tag types in the heaviest exe
    /// against Friflo's 255 of each. Amber past three quarters, red past nine tenths.
    /// </summary>
    private void DrawBudget()
    {
        (string assembly, int components, int tags) = _heaviest;
        float right = Width - Margin, y = 26;
        foreach ((string label, int count, int max, string why) in new[]
                 {
                     ("COMPONENTS", components, CodeMap.MaxComponents, "component types; one more than the limit makes CreateEntity throw"),
                     ("TAGS", tags, CodeMap.MaxTags, "tag types; past the limit tags silently share bits and queries go wrong"),
                 })
        {
            float share = (float)count / max;
            Color colour = share > 0.9f ? Palette.DangerBright : share > 0.75f ? Palette.Warning : Palette.Success;
            var box = new Rect2(right - 300, y, 300, 22);
            Label(new Rect2(box.Position.X, y, 120, 22), label, Palette.TextMuted, 14);
            var bar = new Rect2(box.Position.X + 110, y + 6, 110, 10);
            Canvas.DrawRect(Item, bar, Palette.SurfaceDeep);
            Canvas.DrawRect(Item, new Rect2(bar.Position, new Vector2(bar.Size.X * Math.Min(1, share), bar.Size.Y)), colour);
            Label(new Rect2(bar.End.X + 10, y, 80, 22), $"{count} / {max}", colour, 15);
            if (box.HasPoint(_mouse))
                _hint = $"{count} {why}. Counted for {assembly.Replace("Xanadu.", "")} with what it references, the heaviest exe, Friflo's own included; the limit is per process.";
            y += 24;
        }
    }

    /// <summary>Every type whose name has the search in it, best matches first; click to look at it.</summary>
    private void DrawList()
    {
        List<int> list = Sorted(_map.Nodes.Where(n => _query.Length == 0 || n.Name.Contains(_query, StringComparison.OrdinalIgnoreCase)).Select(n => n.Id));
        Column(ListLeft, ListWidth, Top, list, selected: id => id == _focus || id == _other,
            click: id => Go(id, second: Comparing && _focus >= 0), drawLines: null);
    }

    private void DrawFocus()
    {
        if (_focus < 0)
        {
            Label(new Rect2(ChartLeft, 500, ChartRight - ChartLeft, 40), "pick a type on the left", Palette.TextMuted, 22, center: true);
            return;
        }

        CodeNode node = _map.Nodes[_focus];
        float cardWidth = 300, centreX = (ChartLeft + ChartRight) / 2 - cardWidth / 2;
        var centre = new Rect2(centreX, (Top + Bottom) / 2 - 40, cardWidth, 80);

        float side = 380;
        Groups(ChartLeft, side, node.In, centre, incoming: true);
        Groups(ChartRight - side, side, node.Out, centre, incoming: false);
        BigCard(centre, node);
    }

    /// <summary>A column of one side's links, grouped by kind under a heading each, each card joined to the centre card.</summary>
    private void Groups(float left, float width, Dictionary<LinkKind, SortedSet<int>> links, Rect2 centre, bool incoming)
    {
        var rows = new List<(int Id, LinkKind Kind)>();
        foreach (LinkKind kind in Enum.GetValues<LinkKind>())
        {
            if (!links.TryGetValue(kind, out var ids))
                continue;
            List<int> shown = Sorted(Visible(ids));
            if (shown.Count == 0)
                continue;
            rows.Add((-1 - (int)kind, kind));
            rows.AddRange(shown.Select(id => (id, kind)));
        }

        if (rows.Count == 0)
        {
            Label(new Rect2(left, centre.Position.Y, width, 30), incoming ? "nothing points here" : "points at nothing", Palette.TextDim, 16, center: true);
            return;
        }

        float y = Top;
        int first = Scrolled(left, width, rows.Count);
        Vector2 end = incoming ? new Vector2(centre.Position.X, centre.GetCenter().Y) : new Vector2(centre.End.X, centre.GetCenter().Y);
        for (int r = first; r < rows.Count && y + Card <= Bottom; r++)
        {
            (int id, LinkKind kind) = rows[r];
            if (id < 0)
            {
                Label(new Rect2(left, y, width, Card), Heading(kind, incoming), LinkColours[kind], 14);
                y += Card;
                continue;
            }

            var box = new Rect2(left, y, width, Card);
            Vector2 start = incoming ? new Vector2(box.End.X, box.GetCenter().Y) : new Vector2(box.Position.X, box.GetCenter().Y);
            Canvas.DrawLine(Item, start, end, LinkColours[kind].Lerp(Palette.Background, 0.45f), 1.5f);
            SmallCard(box, _map.Nodes[id], () => Go(id));
            y += Card + CardGap;
        }

        More(left, width, first, rows.Count, y);
    }

    private void DrawCompare()
    {
        if (_focus < 0 || _other < 0 || _focus == _other)
        {
            string ask = _focus < 0 ? "pick a type on the left" : $"pick a type on the left to compare with {_map.Nodes[_focus].Name}";
            Label(new Rect2(ChartLeft, 500, ChartRight - ChartLeft, 40), ask, Palette.TextMuted, 22, center: true);
            return;
        }

        CodeNode a = _map.Nodes[_focus], b = _map.Nodes[_other];
        HashSet<int> near(CodeNode n) => Visible(n.In.Values.Concat(n.Out.Values).SelectMany(set => set)).Where(id => id != a.Id && id != b.Id).ToHashSet();
        HashSet<int> aNear = near(a), bNear = near(b);
        List<int> shared = Sorted(aNear.Intersect(bNear));
        List<int> onlyA = Sorted(aNear.Except(bNear));
        List<int> onlyB = Sorted(bNear.Except(aNear));

        float total = ChartRight - ChartLeft;
        float column = total * 0.2f, cardWidth = total * 0.14f;
        float onlyALeft = ChartLeft, aLeft = onlyALeft + column + 30;
        float sharedLeft = aLeft + cardWidth + 30, bLeft = sharedLeft + column + 30;
        float onlyBLeft = bLeft + cardWidth + 30;
        float centreY = (Top + Bottom) / 2;
        var aCard = new Rect2(aLeft, centreY - 40, cardWidth, 80);
        var bCard = new Rect2(bLeft, centreY - 40, cardWidth, 80);

        Label(new Rect2(ChartLeft, Top - 34, total, 26),
            $"{a.Name} touches {aNear.Count}, {b.Name} touches {bNear.Count}; they share {shared.Count}" + Direct(a, b),
            Palette.Text, 18, center: true);
        Label(new Rect2(onlyALeft, Top - 6, column, 22), $"ONLY {a.Name.ToUpperInvariant()}", Palette.TextMuted, 14);
        Label(new Rect2(sharedLeft, Top - 6, column, 22), "BOTH", Palette.Accent, 14);
        Label(new Rect2(onlyBLeft, Top - 6, ChartRight - onlyBLeft, 22), $"ONLY {b.Name.ToUpperInvariant()}", Palette.TextMuted, 14);

        if (Linked(a, b) is { } direct)
            Canvas.DrawLine(Item, new Vector2(aCard.End.X, centreY), new Vector2(bCard.Position.X, centreY), LinkColours[direct].Lerp(Palette.Background, 0.3f), 2f);
        Column(onlyALeft, column, Top + 20, onlyA, _ => false, id => Go(id), box => Line(box.End.X, box, aCard.Position.X, aCard, a, box));
        Column(sharedLeft, column, Top + 20, shared, _ => false, id => Go(id), box =>
        {
            Line(box.Position.X, box, aCard.End.X, aCard, a, box);
            Line(box.End.X, box, bCard.Position.X, bCard, b, box);
        });
        Column(onlyBLeft, ChartRight - onlyBLeft, Top + 20, onlyB, _ => false, id => Go(id, second: true), box => Line(box.Position.X, box, bCard.End.X, bCard, b, box));
        BigCard(aCard, a);
        BigCard(bCard, b);
        Button(new Rect2(aLeft, aCard.End.Y + 12, cardWidth, 32), "SWAP", false, () => (_focus, _other) = (_other, _focus), "swap the two");
    }

    /// <summary>
    /// SIMILAR: with a component picked, the components most like it; otherwise the
    /// most alike pairs of all. Each row gives the score, then shape and users apart,
    /// then the field types; click a card to compare the two.
    /// </summary>
    private void DrawSimilar()
    {
        float total = ChartRight - ChartLeft;
        CodeNode? picked = _focus >= 0 && _map.Nodes[_focus].Kind == NodeKind.Component ? _map.Nodes[_focus] : null;
        List<Match> matches = picked is null ? _similarity.Pairs() : _similarity.Like(picked.Id);
        string title = picked is null
            ? $"the {matches.Count} most alike component pairs: same field types (shape) and same users; pick a component on the left for its own"
            : $"components like {picked.Name} ({Fields(picked)}): same field types (shape) and same users";
        Label(new Rect2(ChartLeft, Top - 34, total, 26), Fit(title, total, 18), Palette.Text, 18);
        if (matches.Count == 0)
        {
            Label(new Rect2(ChartLeft, 500, total, 40), "nothing shares a field type or a user with it", Palette.TextMuted, 22, center: true);
            return;
        }

        float cardWidth = picked is null ? 300 : 380;
        float y = Top + 20;
        int first = Scrolled(ChartLeft, total, matches.Count);
        for (int i = first; i < matches.Count && y + Card <= Bottom; i++)
        {
            Match match = matches[i];
            float x = ChartLeft;
            if (picked is null)
            {
                Pick(new Rect2(x, y, cardWidth, Card), match.A, match);
                x += cardWidth + 8;
            }

            CodeNode other = _map.Nodes[match.B];
            Pick(new Rect2(x, y, cardWidth, Card), match.B, match);
            x += cardWidth + 16;

            Label(new Rect2(x, y, 60, Card), $"{match.Score * 100:0}%", Palette.TextDim.Lerp(Palette.Accent, match.Score), TextSize);
            Label(new Rect2(x + 60, y, 220, Card), $"shape {match.Shape * 100:0}%  ·  users {match.Users * 100:0}%", Palette.TextMuted, 14);
            float rest = ChartRight - (x + 290);
            string fields = picked is null ? $"{Fields(_map.Nodes[match.A])}  |  {Fields(other)}" : Fields(other);
            Label(new Rect2(x + 290, y, rest, Card), Fit(fields, rest, 14), Palette.TextDim, 14);
            y += Card + CardGap;
        }

        More(ChartLeft, total, first, matches.Count, y);
    }

    /// <summary>One card of a SIMILAR row; a click compares the row's two.</summary>
    private void Pick(Rect2 box, int id, Match match)
    {
        CodeNode node = _map.Nodes[id];
        SmallCard(box, node, () => Open(match.A, match.B));
        if (box.HasPoint(_mouse))
            _hint = $"{node.FullName}  ·  {Fields(node)}  ·  click to compare {_map.Nodes[match.A].Name} with {_map.Nodes[match.B].Name}";
    }

    /// <summary>A component's field types, short: <c>float, Vector2, List&lt;Entity&gt;</c>.</summary>
    private static string Fields(CodeNode node) =>
        node.Fields.Count == 0 ? "no fields" : string.Join(", ", node.Fields.Select(f => Short(f.Type)));

    /// <summary>A full type name without namespaces or arity marks.</summary>
    private static string Short(string type)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace(type, @"[\w.]*\.|`\d+", "").Replace('/', '.');
        return System.Text.RegularExpressions.Regex.Replace(plain, @"\w+", word => Keywords.GetValueOrDefault(word.Value, word.Value));
    }

    private static readonly Dictionary<string, string> Keywords = new()
    {
        ["Single"] = "float", ["Double"] = "double", ["Int32"] = "int", ["UInt32"] = "uint", ["Int64"] = "long",
        ["UInt64"] = "ulong", ["Int16"] = "short", ["UInt16"] = "ushort", ["Byte"] = "byte", ["SByte"] = "sbyte",
        ["Boolean"] = "bool", ["String"] = "string", ["Char"] = "char", ["Object"] = "object",
    };

    /// <summary>A line from a card to a centre card, in the colour of how that centre type touches it.</summary>
    private void Line(float fromX, Rect2 from, float toX, Rect2 to, CodeNode centre, Rect2 card)
    {
        int id = _cardIds.TryGetValue(card.Position, out int known) ? known : -1;
        Color colour = id >= 0 && Linked(centre, _map.Nodes[id]) is { } kind ? LinkColours[kind] : LinkColours[LinkKind.Uses];
        Canvas.DrawLine(Item, new Vector2(fromX, from.GetCenter().Y), new Vector2(toX, to.GetCenter().Y), colour.Lerp(Palette.Background, 0.45f), 1.5f);
    }

    private readonly Dictionary<Vector2, int> _cardIds = new();

    /// <summary>A scrolling column of cards; <paramref name="drawLines"/> draws a card's lines before the card goes over them.</summary>
    private void Column(float left, float width, float top, List<int> ids, Func<int, bool> selected, Action<int> click, Action<Rect2>? drawLines)
    {
        float y = top;
        int first = Scrolled(left, width, ids.Count);
        for (int i = first; i < ids.Count && y + Card <= Bottom; i++)
        {
            int id = ids[i];
            var box = new Rect2(left, y, width, Card);
            _cardIds[box.Position] = id;
            drawLines?.Invoke(box);
            SmallCard(box, _map.Nodes[id], () => click(id), selected(id));
            y += Card + CardGap;
        }

        if (ids.Count == 0)
            Label(new Rect2(left, top, width, Card), "none", Palette.TextDim, 15);
        More(left, width, first, ids.Count, y);
    }

    /// <summary>The first row a column shows: its scroll, kept in range; the pointer over it gets the wheel.</summary>
    private int Scrolled(float left, float width, int count)
    {
        int fits = (int)((Bottom - Top) / (Card + CardGap));
        int first = Math.Clamp(_scroll.GetValueOrDefault(left), 0, Math.Max(0, count - fits + 2));
        _scroll[left] = first;
        if (_mouse.X >= left && _mouse.X < left + width && _mouse.Y >= Top && _mouse.Y < Bottom)
            _wheelColumn = left;
        return first;
    }

    private void More(float left, float width, int first, int count, float y)
    {
        int shown = (int)((y - Top) / (Card + CardGap));
        if (first > 0 || first + shown < count)
            Label(new Rect2(left, Bottom + 2, width, 20), $"{first + 1}–{Math.Min(count, first + shown)} of {count} · wheel to scroll", Palette.TextDim, 13);
    }

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

    private string Direct(CodeNode a, CodeNode b)
    {
        if (a.Out.Any(pair => pair.Value.Contains(b.Id)))
            return $"; {a.Name} {Heading(a.Out.First(pair => pair.Value.Contains(b.Id)).Key, false).ToLowerInvariant()} {b.Name}";
        if (b.Out.Any(pair => pair.Value.Contains(a.Id)))
            return $"; {b.Name} {Heading(b.Out.First(pair => pair.Value.Contains(a.Id)).Key, false).ToLowerInvariant()} {a.Name}";
        return "; neither touches the other";
    }

    private void SmallCard(Rect2 box, CodeNode node, Action click, bool selected = false)
    {
        bool hover = box.HasPoint(_mouse);
        // The whole card in its kind's colour, so systems, components and classes read apart at a glance.
        Color kind = KindColours[node.Kind];
        if (selected)
            Canvas.DrawRect(Item, new Rect2(box.Position - Vector2.One * 2, box.Size + Vector2.One * 4), Palette.TextBright);
        Canvas.DrawRect(Item, box, Palette.Surface.Lerp(kind, hover ? 0.42f : selected ? 0.34f : 0.24f));
        Canvas.DrawRect(Item, new Rect2(box.Position.X, box.Position.Y, 4, box.Size.Y), kind);
        Label(new Rect2(box.Position.X + 12, box.Position.Y, box.Size.X - 16, box.Size.Y), Fit(node.Name, box.Size.X - 16, TextSize), Palette.Text, TextSize);
        if (hover)
        {
            _hint = $"{node.FullName}  ·  {node.Kind.ToString().ToLowerInvariant()}  ·  {node.Assembly}  ·  uses {Degree(node.Out)}, used by {Degree(node.In)}  ·  click to go there";
            _onClick = click;
        }
    }

    private void BigCard(Rect2 box, CodeNode node)
    {
        Canvas.DrawRect(Item, new Rect2(box.Position - Vector2.One * 2, box.Size + Vector2.One * 4), KindColours[node.Kind]);
        Canvas.DrawRect(Item, box, Palette.SurfaceRaised.Lerp(KindColours[node.Kind], 0.24f));
        Label(new Rect2(box.Position.X + 12, box.Position.Y + 6, box.Size.X - 24, 30), Fit(node.Name, box.Size.X - 24, 22), Palette.TextBright, 22);
        Label(new Rect2(box.Position.X + 12, box.Position.Y + 38, box.Size.X - 24, 18), node.Kind.ToString().ToLowerInvariant() + "  ·  " + node.Assembly.Replace("Xanadu.", ""), KindColours[node.Kind], 14);
        Label(new Rect2(box.Position.X + 12, box.Position.Y + 56, box.Size.X - 24, 18), Fit(node.Namespace, box.Size.X - 24, 13), Palette.TextDim, 13);
        if (box.HasPoint(_mouse))
            _hint = node.FullName;
    }

    // ---- bits -----------------------------------------------------------------------

    private void Button(Rect2 box, string label, bool on, Action click, string tooltip, int size = 17)
    {
        bool hover = box.HasPoint(_mouse);
        // Bright blue only for what's chosen; muted at rest.
        Color fill = on ? Palette.Primary : Palette.Muted;
        Canvas.DrawRect(Item, box, hover ? fill.Lerp(Colors.White, 0.16f) : fill);
        Label(box, label, Palette.TextBright, size, center: true);
        if (hover)
        {
            _hint = tooltip;
            _onClick = click;
        }
    }

    private void DrawHint(string hint)
    {
        var box = new Rect2(Margin, Height - 44, Width - Margin * 2, 32);
        Canvas.DrawRect(Item, box, Palette.Tooltip);
        Label(new Rect2(box.Position.X + 12, box.Position.Y, box.Size.X - 24, box.Size.Y), hint, Palette.Text, 16);
    }

    private void Label(Rect2 box, string text, Color colour, int size, bool center = false)
    {
        if (center)
            Canvas.DrawTextStyled(Item, box, text, colour, size);
        else
            Canvas.DrawTextStyledLeft(Item, box, text, colour, size);
    }

    /// <summary>Cuts a name to what fits <paramref name="width"/> at <paramref name="size"/>, keeping its start.</summary>
    private static string Fit(string text, float width, int size)
    {
        int max = Math.Max(4, (int)(width / (size * 0.55f)));
        return text.Length <= max ? text : text[..(max - 1)] + "…";
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
        return true;
    }

    /// <summary>Typing finds a type while the tab is open.</summary>
    public override void Shown() => TextInput.Claimed = true;

    /// <summary>Typing goes back to the UI once the tab closes.</summary>
    public override void Hidden() => TextInput.Claimed = false;

    public override void Dispose()
    {
        Hidden();
        base.Dispose();
    }
}

/// <summary>How Atlas opens: on a type, comparing two, sorted a way, or on SIMILAR.</summary>
internal readonly record struct AtlasOptions(
    string? Focus = null,
    string? Compare = null,
    AtlasStage.SortMode Sort = AtlasStage.SortMode.Name,
    bool Reverse = false,
    bool Similar = false);
