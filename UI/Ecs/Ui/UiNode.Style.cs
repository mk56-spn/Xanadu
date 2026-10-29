using System;
using Friflo.Engine.ECS;
using Godot;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ecs;

/// <summary>
/// Chains the geometry of a node: its size, where it sits, and how its
/// children stack. Each call edits the entity's components and returns the
/// node, so a tree can be both shaped and filled in one pass.
/// </summary>
public readonly partial struct UiNode
{
    // ------------------------------------------------------------- geometry

    /// <summary>Fixes both dimensions, cancelling any auto sizing on them.</summary>
    public UiNode Size(float width, float height) =>
        Fixed(width, height, fixWidth: true, fixHeight: true);

    /// <summary>Fixes both dimensions, cancelling any auto sizing on them.</summary>
    public UiNode Size(Vector2 size) =>
        Fixed(size.X, size.Y, fixWidth: true, fixHeight: true);

    /// <summary>Fixes the width; the height keeps its auto sizing.</summary>
    public UiNode Width(float width) =>
        Fixed(width, null, fixWidth: true, fixHeight: false);

    /// <summary>Fixes the height; the width keeps its auto sizing.</summary>
    public UiNode Height(float height) =>
        Fixed(null, height, fixWidth: false, fixHeight: true);

    /// <summary>Places and sizes the node from a screen rectangle, in one call.</summary>
    public UiNode Box(Rect2 box)
    {
        if (IsNull)
            return this;

        Fixed(box.Size.X, box.Size.Y, fixWidth: true, fixHeight: true);
        return At(box.GetCenter());
    }

    /// <summary>Fills the given screen with the node, centred on it.</summary>
    public UiNode Cover(Vector2 screen) => Box(new Rect2(Vector2.Zero, screen));

    /// <summary>Moves the node's centre to a parent-local point.</summary>
    public UiNode At(Vector2 center)
    {
        if (IsNull || !_entity.TryGetComponent(out Position position))
            return this;

        if (position.Value == center)
            return this;

        position.Value = center;
        _entity.AddComponent(position);
        _entity.AddTag<Moved>();
        return this;
    }

    /// <summary>Moves the node by a delta, for nudges and slides.</summary>
    public UiNode Move(Vector2 delta) =>
        IsNull || !_entity.TryGetComponent(out Position position)
            ? this
            : At(position.Value + delta);

    /// <summary>Lets the container's width follow its children again.</summary>
    public UiNode AutoWidth(bool auto = true)
    {
        if (IsNull)
            return this;

        Container.AutoWidth = auto;
        return Touch(layout: true);
    }

    /// <summary>Lets the container's height follow its children again.</summary>
    public UiNode AutoHeight(bool auto = true)
    {
        if (IsNull)
            return this;

        Container.AutoHeight = auto;
        return Touch(layout: true);
    }

    /// <summary>Lets both dimensions follow the children.</summary>
    public UiNode AutoSize(bool auto = true)
    {
        if (IsNull)
            return this;

        Container.AutoWidth = auto;
        Container.AutoHeight = auto;
        return Touch(layout: true);
    }

    // ---------------------------------------------------------------- stack

    /// <summary>Gap between children along the stacking direction.</summary>
    public UiNode Gap(float spacing)
    {
        if (IsNull)
            return this;

        Container.Spacing = spacing;
        return Touch(layout: true);
    }

    /// <summary>Inset between the container's edge and its children.</summary>
    public UiNode Pad(float padding)
    {
        if (IsNull)
            return this;

        Container.Padding = padding;
        return Touch(layout: true);
    }

    /// <summary>Gap between grid rows; a negative value uses the column gap.</summary>
    public UiNode LineGap(float lineSpacing)
    {
        if (IsNull)
            return this;

        Container.LineSpacing = lineSpacing;
        return Touch(layout: true);
    }

    /// <summary>Shear in radians, for slanted strips.</summary>
    public UiNode Skew(float radians)
    {
        if (IsNull)
            return this;

        Container.Skew = radians;
        return Touch(layout: true);
    }

    /// <summary>Where children sit along the stacking direction.</summary>
    public UiNode Main(StackMainAlignment align)
    {
        if (IsNull)
            return this;

        Container.Main = align;
        return Touch(layout: true);
    }

    /// <summary>Where children sit across the stacking direction.</summary>
    public UiNode Cross(StackCrossAlignment align)
    {
        if (IsNull)
            return this;

        Container.Cross = align;
        return Touch(layout: true);
    }

    /// <summary>Turns the stack into a grid of <paramref name="columns"/> columns.</summary>
    public UiNode Columns(int columns)
    {
        if (IsNull)
            return this;

        Container.Columns = Math.Max(1, columns);
        return Touch(layout: true);
    }

    /// <summary>Clips children to the container's bounds.</summary>
    public UiNode Clip(bool clip = true)
    {
        if (IsNull)
            return this;

        Container.Clip = clip;
        return Touch(layout: false);
    }

    /// <summary>Keeps the top edge fixed while an auto height resizes.</summary>
    public UiNode AnchorTop(bool anchor = true)
    {
        if (IsNull)
            return this;

        Container.AnchorTop = anchor;
        return Touch(layout: true);
    }

    /// <summary>Keeps the left edge fixed while an auto width resizes.</summary>
    public UiNode AnchorLeft(bool anchor = true)
    {
        if (IsNull)
            return this;

        Container.AnchorLeft = anchor;
        return Touch(layout: true);
    }

    // ------------------------------------------------------------- placement

    /// <summary>
    /// Anchors the node to a point of its parent and takes it out of the
    /// parent's flow. Anchoring and ignoring the layout always travel together,
    /// so this sets both.
    /// </summary>
    public UiNode Anchor(Vector2 anchor, Vector2? pivot = null)
    {
        if (IsNull)
            return this;

        _entity.AddComponent(new UiAnchor(anchor, pivot ?? anchor));
        return IgnoreLayout();
    }

    /// <summary>Anchors the node to an edge or corner of its parent.</summary>
    public UiNode Anchor(UiEdge edge) => Anchor(edge.Anchor());

    /// <summary>Includes or excludes the node from its parent's stack.</summary>
    public UiNode IgnoreLayout(bool ignored = true)
    {
        if (IsNull)
            return this;

        UiLayout.SetIgnored(_entity, ignored);
        return this;
    }

    // ------------------------------------------------------------- machinery

    /// <summary>The node's container component, for the stack methods.</summary>
    private ref UiContainer Container
    {
        get
        {
            if (IsNull)
                throw new InvalidOperationException(
                    "A null UI node has no container component.");

            if (!_entity.HasComponent<UiContainer>())
                throw new InvalidOperationException(
                    "This call needs a container. Build the node with " +
                    "Column, Row, Grid, Panel or Edge first.");

            return ref _entity.GetComponent<UiContainer>();
        }
    }

    private UiNode Fixed(
        float? width,
        float? height,
        bool fixWidth,
        bool fixHeight)
    {
        if (IsNull)
            return this;

        Vector2 size = _entity.GetComponent<Rectangle>().Bounds.Size;
        if (width is { } w)
            size.X = w;
        if (height is { } h)
            size.Y = h;

        SetBounds(size);

        if (_entity.TryGetComponent(out UiContainer container))
        {
            bool changed = false;
            if (fixWidth && container.AutoWidth)
            {
                container.AutoWidth = false;
                changed = true;
            }

            if (fixHeight && container.AutoHeight)
            {
                container.AutoHeight = false;
                changed = true;
            }

            if (changed)
                _entity.AddComponent(container);
        }

        return Touch(layout: true);
    }

    private void SetBounds(Vector2 size)
    {
        ref Rectangle rectangle = ref _entity.GetComponent<Rectangle>();
        rectangle.Bounds = new Rect2(-size / 2, size);
    }
}
