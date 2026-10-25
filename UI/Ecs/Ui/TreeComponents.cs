using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>
/// One node of a tree, given in any order: <see cref="Parent"/> is the id of
/// another item, or null for a root. Ids must be unique within the tree.
/// </summary>
public readonly record struct UiTreeItem(string Id, string? Parent, string Label);

/// <summary>State of a tree control; lives on the tree container entity.</summary>
public struct UiTree : IComponent
{
    /// <summary>Ids of nodes whose children are hidden.</summary>
    public HashSet<string> Collapsed;

    /// <summary>Id of the selected node, or empty for none.</summary>
    public string Selected;

    public float Width;
    public float RowHeight;
    public float Indent;

    public static UiTree Create(float width, float rowHeight, float indent) => new()
    {
        Collapsed = new HashSet<string>(StringComparer.Ordinal),
        Selected = "",
        Width = width,
        RowHeight = rowHeight,
        Indent = indent,
    };
}

/// <summary>A row of a tree, on the row container entity.</summary>
public struct UiTreeNode : IComponent
{
    public Entity Tree;
    public string Id;

    /// <summary>Id of the parent node, or empty for a root.</summary>
    public string Parent;
    public int Depth;
    public bool HasChildren;

    /// <summary>The expand/collapse arrow; present on every row so labels stay aligned.</summary>
    public Entity Disclosure;
    public Entity Label;
}

/// <summary>Marks the arrow button that expands or collapses a node.</summary>
public struct UiTreeDisclosure(Entity node) : IComponent
{
    public Entity Node = node;
}

/// <summary>Marks the label button that selects a node.</summary>
public struct UiTreeLabel(Entity node) : IComponent
{
    public Entity Node = node;
}

/// <summary>Set on the tree entity when the user selects a node; the consumer removes it.</summary>
public struct UiTreeChanged(string id) : IComponent
{
    public string Id = id;
}
