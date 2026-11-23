using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// One node of a tree, given in any order: <see cref="Parent"/> is the id of
/// another item, or null for a root. Ids must be unique within the tree.
/// <see cref="Cells"/> fill the tree's right-aligned columns, when it has any.
/// <see cref="Tint"/> colours the label, so a tree holding several kinds of
/// thing can tell them apart; null keeps the theme's text colour.
/// <see cref="Badges"/> are small shapes after the label, marking something about
/// the node at a glance; their tips together make the label's tooltip.
/// </summary>
public readonly record struct UiTreeItem(
    string Id, string? Parent, string Label, string[]? Cells = null, Color? Tint = null, UiTreeBadge[]? Badges = null);

/// <summary>A shape after a tree row's label (see <see cref="UiShapes"/>), outlined unless <see cref="Filled"/>.</summary>
public readonly record struct UiTreeBadge(UiSdfShape Shape, Color Color, bool Filled = true, string? Tip = null);

/// <summary>State of a tree control; lives on the tree container entity.</summary>
public struct UiTree : IComponent
{
    /// <summary>Id of the selected node, or empty for none.</summary>
    public string Selected;

    /// <summary>Ids drawn as selected beside <see cref="Selected"/>, for multi-select trees.</summary>
    public HashSet<string> MultiSelected;

    public float Width;
    public float RowHeight;
    public float Indent;

    /// <summary>Widths of the columns right of every label, or null for none.</summary>
    public float[]? CellWidths;

    public static UiTree Create(float width, float rowHeight, float indent, float[]? cellWidths = null) => new()
    {
        Selected = "",
        MultiSelected = new HashSet<string>(StringComparer.Ordinal),
        Width = width,
        RowHeight = rowHeight,
        Indent = indent,
        CellWidths = cellWidths,
    };
}

/// <summary>
/// A node of a tree. The hierarchy is the entity tree: a node's first child is
/// its row and, when it has children, its second is the body holding their nodes.
/// A collapsed node's body is hidden.
/// </summary>
public struct UiTreeNode : IComponent
{
    public string Id;

    /// <summary>The badges its row was built with, so a change rebuilds the row.</summary>
    public UiTreeBadge[]? Badges;
}

/// <summary>Marks the arrow button that expands or collapses a node.</summary>
public struct UiTreeDisclosure : ITag;

/// <summary>Marks the label button that selects a node.</summary>
public struct UiTreeLabel : ITag;

/// <summary>
/// Signal emitted on the tree entity when a node is selected with
/// <c>raiseChanged</c> (a user click). Not a component: listen with
/// <see cref="UiTreeControl.OnSelected"/>.
/// </summary>
public readonly struct TreeNodeSelected(string id)
{
    public readonly string Id = id;
}

/// <summary>Marks the entity inspected in the F4 UI tree, for the bounds overlay to colour.</summary>
public struct UiTreeOverlaySelected : ITag;

/// <summary>Marks the entity under the cursor in F4's pick mode, or behind the hovered tree row.</summary>
public struct UiTreeOverlayHovered : ITag;
