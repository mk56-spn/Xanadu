using System;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A handle on one UI entity that can create its own children. Every method
/// that adds something returns the node it added, so a tree is built by naming
/// each parent once and calling on it:
/// <code>
/// UiNode page = UiNode.Layer(store, root, 960).Page(screen);
/// UiNode header = page.Row().Height(80);
/// UiNode left = header.Edge(UiEdge.Left).Gap(12);
/// left.Text("LEVEL EDITOR", 24).TextColor(Palette.Accent);
/// left.IconButton("UN", "res://Assets/Icons/undo.svg").On(EditorButtonKind.Undo);
/// </code>
/// There is no cursor and no <c>End()</c>: a child is added to the node it was
/// called on, never to "whatever was opened last". That removes the matching
/// stack a builder needs, and lets a subtree be extended later by keeping its
/// node.
/// </summary>
/// <remarks>
/// Styling methods mutate the entity's components in place and mark it dirty,
/// so a chain reads top to bottom and each step is a plain ECS edit. They are
/// no-ops on a default node, which lets a chain that lost its parent degrade to
/// nothing instead of throwing; creation methods throw instead.
/// </remarks>
public readonly partial struct UiNode
{
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private readonly UiSurface? _surface;
    private readonly Entity _entity;

    internal UiNode(UiSurface surface, Entity entity)
    {
        _surface = surface;
        _entity = entity;
    }

    /// <summary>The wrapped entity.</summary>
    public Entity Entity => _entity;

    /// <summary>True when this node came from <c>default</c> or a deleted entity.</summary>
    public bool IsNull => _surface is null || _entity.IsNull;

    /// <summary>The surface the node belongs to.</summary>
    public UiSurface Surface => _surface
        ?? throw new InvalidOperationException(
            "The UI node is null, so it has no surface. Build it from Layer(...).");

    /// <summary>A node on the entity's parent, or a null node at the top.</summary>
    public UiNode Parent => IsNull || _entity.Parent.IsNull
        ? default
        : new UiNode(_surface!, _entity.Parent);

    /// <summary>Lets a node stand in wherever the raw entity was expected.</summary>
    public static implicit operator Entity(UiNode node) => node._entity;

    /// <summary>The design frame the tree is authored against.</summary>
    public UiStage Stage => Surface.Stage;

    /// <summary>
    /// Starts a tree: creates the layer at <paramref name="zIndex"/>, parents it
    /// under <paramref name="parent"/> when one is given, and returns its root.
    /// </summary>
    public static UiNode Layer(
        EntityStore entities,
        Entity parent,
        int zIndex,
        IVisualRidFactory? visuals = null,
        UiStage? stage = null)
    {
        UiBuilder builder = UiBuilder.Begin(entities, visuals).Layer(zIndex);
        Entity layer = builder.Current;
        if (!parent.IsNull)
            parent.AddChild(layer);

        var surface = new UiSurface(entities, builder, layer, stage ?? UiStage.Full);
        return new UiNode(surface, layer);
    }

    /// <summary>
    /// Continues an existing entity, such as a panel built earlier or one a
    /// system created by hand. New children land under it; its own layer is
    /// found from its drawable.
    /// </summary>
    public static UiNode Wrap(Entity entity)
    {
        if (entity.IsNull)
            throw new ArgumentException("Cannot wrap a null entity.", nameof(entity));

        Entity layer = entity;
        while (!layer.IsNull && !layer.HasComponent<Layer>())
            layer = layer.Parent;

        UiBuilder builder = UiBuilder.Begin(entity.Store);
        builder.Use(entity);
        var surface = new UiSurface(
            entity.Store,
            builder,
            layer.IsNull ? entity : layer,
            UiStage.Full);
        return new UiNode(surface, entity);
    }

    /// <summary>The layer this node draws on, for spawning floating parts.</summary>
    public UiNode LayerNode => IsNull ? default : new UiNode(_surface!, _surface!.Layer);

    // ---------------------------------------------------------------- layout

    /// <summary>A full-screen root that stacks vertically from the top.</summary>
    public UiNode Page() =>
        Stack(StackDirection.Vertical).Box(Stage.Bounds);

    /// <summary>A full-screen root on an explicit surface, for odd hosts.</summary>
    public UiNode Page(Vector2 screen) =>
        Stack(StackDirection.Vertical).Box(new Rect2(Vector2.Zero, screen));

    /// <summary>A container that stacks its children vertically, sized to them.</summary>
    public UiNode Column() => Stack(StackDirection.Vertical);

    /// <summary>A container that stacks its children horizontally, sized to them.</summary>
    public UiNode Row() => Stack(StackDirection.Horizontal);

    /// <summary>A container of <paramref name="columns"/> columns filling row by row.</summary>
    public UiNode Grid(int columns, float gap = 0, float rowGap = -1)
    {
        UiBuilder builder = BeginChildren();
        builder.Container(
            Vector2.Zero,
            Vector2.Zero,
            StackDirection.Horizontal,
            spacing: gap,
            color: Transparent,
            autoWidth: true,
            autoHeight: true,
            cross: StackCrossAlignment.Center,
            main: StackMainAlignment.Start,
            columns: Math.Max(1, columns),
            lineSpacing: rowGap);
        return EndChildren(builder);
    }

    /// <summary>A themed panel: padding, spacing and a surface fill.</summary>
    public UiNode Panel(Color? fill = null)
    {
        UiBuilder builder = BeginChildren();
        builder.Container(
            Vector2.Zero,
            Vector2.Zero,
            StackDirection.Vertical,
            spacing: UiTheme.ContainerSpacing,
            color: fill ?? UiTheme.Panel,
            takesInput: true,
            padding: UiTheme.ContainerPadding,
            cross: StackCrossAlignment.Center,
            autoWidth: true,
            autoHeight: true,
            main: StackMainAlignment.Start);
        return EndChildren(builder);
    }

    /// <summary>
    /// A group that hugs one edge of <paramref name="parent"/> and is skipped by
    /// its parent's flow. It sizes to its children, so an edge that is taller
    /// than the group centres the group rather than needing a padding shim.
    /// </summary>
    public UiNode Edge(UiEdge edge, float gap = 0)
    {
        StackDirection direction = edge.IsVertical()
            ? StackDirection.Vertical
            : StackDirection.Horizontal;

        UiNode group = Stack(direction).Gap(gap);
        group.Anchor(edge.Anchor());
        return group;
    }

    /// <summary>
    /// A clipped viewport with a content column, returned through
    /// <paramref name="content"/>. The viewport scrolls the content's height.
    /// </summary>
    public UiNode Scroll(Vector2 size, float contentHeight, out UiNode content)
    {
        UiBuilder builder = BeginChildren();
        builder.Container(
            Vector2.Zero,
            size,
            StackDirection.Vertical,
            spacing: 0,
            clip: true,
            color: Transparent,
            takesInput: true);
        Entity viewport = builder.Current;

        builder.Container(
            new Vector2(0, -size.Y / 2 + contentHeight / 2),
            new Vector2(size.X, contentHeight),
            StackDirection.Vertical,
            spacing: 4,
            color: Transparent,
            autoHeight: false);
        content = new UiNode(_surface!, builder.Current);

        viewport.AddComponent(new ScrollView(content.Entity));
        builder.End();
        builder.End();

        return new UiNode(_surface!, viewport);
    }

    // ------------------------------------------------------------- machinery

    private UiNode Stack(StackDirection direction)
    {
        UiBuilder builder = BeginChildren();
        builder.Container(
            Vector2.Zero,
            Vector2.Zero,
            direction,
            color: Transparent,
            cross: StackCrossAlignment.Center,
            autoWidth: true,
            autoHeight: true,
            main: StackMainAlignment.Start);
        return EndChildren(builder);
    }

    /// <summary>Points the shared builder at this node's entity.</summary>
    internal UiBuilder BeginChildren()
    {
        if (IsNull)
            throw new InvalidOperationException(
                "The UI node is null; a parent node is required to add children.");

        UiBuilder builder = _surface!.Builder;
        builder.Use(_entity);
        return builder;
    }

    /// <summary>Wraps a leaf entity the builder just made under this node.</summary>
    internal UiNode Leaf(Entity created) => new(_surface!, created);

    /// <summary>Takes the container the builder just made and returns to this node.</summary>
    internal UiNode EndChildren(UiBuilder builder)
    {
        Entity created = builder.Current;
        builder.End();
        return new UiNode(_surface!, created);
    }

    /// <summary>Flags a change: redraw always, and re-layout when geometry moved.</summary>
    internal UiNode Touch(bool layout)
    {
        if (IsNull)
            return this;

        _entity.AddTag<Dirty>();

        if (!layout)
            return this;

        if (_entity.HasComponent<UiContainer>())
            _entity.AddTag<LayoutDirty>();

        Entity parent = _entity.Parent;
        if (!parent.IsNull && parent.HasComponent<UiContainer>())
            parent.AddTag<LayoutDirty>();

        return this;
    }

    public override string ToString() => IsNull ? "UiNode(null)" : $"UiNode({_entity.Id})";
}
