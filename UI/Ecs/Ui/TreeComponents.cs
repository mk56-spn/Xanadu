using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// One node of a tree, given in any order: <see cref="Parent"/> is the id of
/// another item, or null for a root. Ids must be unique within the tree.
/// <see cref="Cells"/> fill the tree's right-aligned columns, when it has any.
/// </summary>
public readonly record struct UiTreeItem(string Id, string? Parent, string Label, string[]? Cells = null);

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
}

/// <summary>Marks the arrow button that expands or collapses a node.</summary>
public struct UiTreeDisclosure : ITag;

/// <summary>Marks the label button that selects a node.</summary>
public struct UiTreeLabel : ITag;

/// <summary>Set on the tree entity when the user selects a node; the consumer removes it.</summary>
public struct UiTreeChanged(string id) : IComponent
{
    public string Id = id;
}

/// <summary>Marks the entity picked in the F4 UI tree, for the bounds overlay to colour.</summary>
public struct UiTreeOverlaySelected : ITag;
