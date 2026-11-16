using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// The tree list: the root's subtree walked through the open branches (or, while
/// searching, every match under the root), drawn into the pooled rows in place.
/// </summary>
public sealed partial class UiTreeOverlaySystem
{
    /// <summary>Components every drawn entity carries; they say nothing about what it is.</summary>
    private static readonly HashSet<Type> Plumbing = new()
    {
        typeof(Position), typeof(Rectangle), typeof(VisualRid), typeof(Drawable),
        typeof(FillColor), typeof(TreeNode), typeof(EntityName), typeof(Text),
        typeof(TextStyle), typeof(TextAlignment), typeof(UiFlex), typeof(UiCommand),
    };

    private readonly HashSet<int> _expanded = new();
    private readonly List<(Entity Entity, int Depth)> _listed = new();
    private readonly List<Entity> _rowEntities = new();
    private readonly List<RowParts> _rowParts = new();
    private readonly List<RowLook?> _rowLooks = new();
    private readonly Dictionary<Archetype, string> _kinds = new();
    private readonly List<Entity> _roots = new();
    private readonly HashSet<Entity> _rootSet = new();
    private bool _toTop;
    private bool? _everyRoot;
    private bool _revealInspected;
    private string _query = "";

    private Entity RowEntity(int pooled) =>
        pooled >= 0 && pooled < _rowEntities.Count ? _rowEntities[pooled] : default;

    /// <summary>The entity behind the tree row under the pointer, so the screen shows which it is.</summary>
    private Entity HoveredRow()
    {
        for (int i = 0; i < _rowParts.Count && i < _rowEntities.Count; i++)
        {
            RowParts parts = _rowParts[i];
            if (parts.Name.Node.Entity.Tags.Has<Hovered>() || parts.Arrow.Node.Entity.Tags.Has<Hovered>())
                return _rowEntities[i];
        }
        return default;
    }

    private void ShowRows()
    {
        _query = _search.Node.Entity.GetComponent<TextField>().Value.Trim();

        _listed.Clear();
        if (_query.Length > 0)
        {
            foreach (Entity top in Tops())
                Match(top);
        }
        else
        {
            foreach (Entity top in Tops())
                Walk(top, 0);
        }

        UiList view = _list.Node.Entity.GetComponent<UiList>();
        if (view.Count != _listed.Count || _toTop)
            UiLists.SetCount(_list, _listed.Count, keepPlace: !_toTop);
        _toTop = false;

        if (_revealInspected)
        {
            _revealInspected = false;
            int at = _listed.FindIndex(row => row.Entity == _inspected);
            if (at >= 0)
                UiLists.Reveal(_list, Math.Max(0, at - 3));
        }

        FillRows();
        _path.Node.SetText(PathText());
        if (_everyRoot != _root.IsNull)
        {
            _everyRoot = _root.IsNull;
            _up.Node.Disabled(_root.IsNull);
            _all.Node.Chosen(_root.IsNull);
        }
    }

    /// <summary>The root alone, or every top-level entity there is when there is no root.</summary>
    private List<Entity> Tops()
    {
        _roots.Clear();
        if (!_root.IsNull)
        {
            _roots.Add(_root);
            return _roots;
        }

        _rootSet.Clear();
        foreach (Entity entity in Query.Store.Entities)
        {
            Entity top = entity;
            while (!top.Parent.IsNull)
                top = top.Parent;
            if (Listable(top) && _rootSet.Add(top))
                _roots.Add(top);
        }

        // Ids keep the listing still while archetypes shuffle.
        _roots.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return _roots;
    }

    /// <summary>Things on screen: anything drawn or placed, and never the debug views.</summary>
    private static bool Listable(Entity entity) =>
        !entity.Tags.Has<DebugUi>() &&
        (entity.HasComponent<Position>() || entity.HasComponent<VisualRid>() || entity.ChildCount > 0);

    private void Walk(Entity entity, int depth)
    {
        if (entity.Tags.Has<DebugUi>())
            return;

        _listed.Add((entity, depth));
        if (!_expanded.Contains(entity.Id))
            return;

        foreach (Entity child in entity.ChildEntities)
            Walk(child, depth + 1);
    }

    private void Match(Entity entity)
    {
        if (entity.Tags.Has<DebugUi>())
            return;

        if (Name(entity).Contains(_query, StringComparison.OrdinalIgnoreCase))
            _listed.Add((entity, 0));
        foreach (Entity child in entity.ChildEntities)
            Match(child);
    }

    private bool HasListedChild(Entity entity)
    {
        foreach (Entity child in entity.ChildEntities)
        {
            if (!child.Tags.Has<DebugUi>())
                return true;
        }
        return false;
    }

    /// <summary>Draws the rows the list shows; runs every frame, since what they name changes live.</summary>
    private void FillRows()
    {
        if (_list is null || _list.Node.IsNull)
            return;

        UiList view = _list.Node.Entity.GetComponent<UiList>();
        _rowEntities.Clear();
        while (_rowLooks.Count < _rowParts.Count)
            _rowLooks.Add(null);
        for (int i = 0; i < view.Shown && i < _rowParts.Count; i++)
        {
            int index = view.First + i;
            if (index >= _listed.Count)
                break;

            (Entity entity, int depth) = _listed[index];
            _rowEntities.Add(entity);
            RowParts parts = _rowParts[i];

            float indent = 1 + depth * Indent;
            if (parts.Indent.Node.Entity.TryGetComponent(out Rectangle box) && Math.Abs(box.Bounds.Size.X - indent) > 0.5f)
                parts.Indent.Node.Width(indent);

            bool branch = HasListedChild(entity);
            parts.Arrow.Node.SetText(!branch ? "" : _expanded.Contains(entity.Id) ? "▾" : "▸");

            string name = Name(entity);
            if (_query.Length > 0 && !entity.Parent.IsNull)
                name += "   in " + Name(entity.Parent);
            else if (branch && !_expanded.Contains(entity.Id))
                name += $"   ({entity.ChildCount})";
            UiNode label = parts.Name.Node.SetText(name);

            // Toning resets a button's hover, so a row is toned only when its look changes.
            var look = new RowLook(entity == _inspected, IsShown(entity));
            if (_rowLooks[i] != look)
            {
                _rowLooks[i] = look;
                if (look.Inspected)
                    label.Chosen(true);
                else
                    label.Quiet();
                if (!look.Inspected)
                    label.TextColor(look.Shown ? label.Roles.Text : label.Roles.TextDim);
            }
            parts.Size.Node.SetText(SizeOf(entity));
        }

        _foot.Node.SetText(_listed.Count == 0
            ? _query.Length > 0 ? "NOTHING MATCHES" : "NOTHING HERE"
            : $"ROWS {view.First + 1}–{view.First + Math.Min(view.Shown, _listed.Count - view.First)} OF {_listed.Count}   ▸ OPENS   NAME INSPECTS, AGAIN MAKES IT THE ROOT");
    }

    /// <summary>Whether the entity is drawn: neither it nor anything above it is hidden or switched off.</summary>
    private static bool IsShown(Entity entity)
    {
        for (Entity up = entity; !up.IsNull; up = up.Parent)
        {
            if (!up.Enabled || UiLayout.IsHidden(up))
                return false;
        }
        return true;
    }

    /// <summary>Where the root sits: its ancestors, outermost first.</summary>
    private string PathText()
    {
        if (_root.IsNull)
            return "EVERY ROOT";

        var parts = new List<string>();
        for (Entity up = _root; !up.IsNull; up = up.Parent)
            parts.Add(Name(up));
        parts.Reverse();
        return string.Join("  ›  ", parts);
    }

    /// <summary>What a row calls an entity: what it is, what it says, and its id.</summary>
    private string Name(Entity entity)
    {
        string kind = KindOf(entity);
        string? words = entity.TryGetComponent(out EntityName name) && !string.IsNullOrWhiteSpace(name.value)
            ? name.value
            : entity.TryGetComponent(out Text text) && !string.IsNullOrWhiteSpace(text.Value)
                ? text.Value
                : null;
        return words is null
            ? $"{kind}  #{entity.Id}"
            : $"{kind}  \"{Shorten(words.ReplaceLineEndings(" "))}\"  #{entity.Id}";
    }

    private static string Shorten(string words) => words.Length > 40 ? words[..39] + "…" : words;

    private static string SizeOf(Entity entity) =>
        entity.TryGetComponent(out Rectangle rect) && rect.Bounds.Size != Vector2.Zero
            ? string.Create(CultureInfo.InvariantCulture, $"{rect.Bounds.Size.X:0}×{rect.Bounds.Size.Y:0}")
            : "";

    /// <summary>What an entity is, from its most telling component.</summary>
    private string KindOf(Entity entity)
    {
        if (entity.TryGetComponent(out Layer layer))
            return $"LAYER {layer.ZIndex}";
        if (entity.TryGetComponent(out UiContainer container))
        {
            string stack = container.Columns > 0
                ? $"GRID {container.Columns}"
                : container.Direction == StackDirection.Vertical ? "COLUMN" : "ROW";
            return entity.HasComponent<ButtonVisual>() ? stack + " BUTTON" : stack;
        }
        if (entity.HasComponent<ButtonVisual>())
            return "BUTTON";

        Archetype archetype = entity.Archetype;
        if (_kinds.TryGetValue(archetype, out string? kind))
            return kind;

        kind = entity.HasComponent<Text>() ? "TEXT" : entity.HasComponent<Rectangle>() ? "RECT" : "ENTITY";
        foreach (ComponentType type in archetype.ComponentTypes)
        {
            if (Plumbing.Contains(type.Type))
                continue;

            kind = Shout(type.Name);
            break;
        }
        _kinds[archetype] = kind;
        return kind;
    }

    /// <summary>UiSliderFill → UI SLIDER FILL, so type names read like the panel's other words.</summary>
    private static string Shout(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                builder.Append(' ');
            builder.Append(char.ToUpperInvariant(c));
        }
        return builder.ToString();
    }

    private readonly record struct RowLook(bool Inspected, bool Shown);
}
