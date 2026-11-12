using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// How a node sizes inside its parent's stack (<see cref="UiFlex"/>): grow
/// along the stack, stretch across it, and min and max sizes. These describe
/// the child, so they are called on the child, not on the container.
/// </summary>
public readonly partial struct UiNode
{
    /// <summary>
    /// Takes a share of the room the stack has left along its axis, by
    /// <paramref name="weight"/>, on top of the node's own size. Equal weights
    /// on equal nodes share evenly; a node with a max stops there.
    /// </summary>
    public UiNode Grow(float weight = 1)
    {
        if (IsNull)
            return this;

        Flex.Grow = weight;
        return Touch(layout: true);
    }

    /// <summary>Fills the stack across its axis, inside its padding.</summary>
    public UiNode Stretch(bool stretch = true)
    {
        if (IsNull)
            return this;

        Flex.Stretch = stretch;
        return Touch(layout: true);
    }

    /// <summary>The narrowest the node gets, grown, stretched or auto-sized.</summary>
    public UiNode MinWidth(float width) => Limits(minX: width);

    /// <summary>The shortest the node gets, grown, stretched or auto-sized.</summary>
    public UiNode MinHeight(float height) => Limits(minY: height);

    /// <summary>The widest the node gets, grown, stretched or auto-sized.</summary>
    public UiNode MaxWidth(float width) => Limits(maxX: width);

    /// <summary>The tallest the node gets, grown, stretched or auto-sized.</summary>
    public UiNode MaxHeight(float height) => Limits(maxY: height);

    /// <summary>The smallest the node gets on both axes.</summary>
    public UiNode MinSize(float width, float height) => Limits(minX: width, minY: height);

    /// <summary>The largest the node gets on both axes.</summary>
    public UiNode MaxSize(float width, float height) => Limits(maxX: width, maxY: height);

    private UiNode Limits(float? minX = null, float? minY = null, float? maxX = null, float? maxY = null)
    {
        if (IsNull)
            return this;

        ref UiFlex flex = ref Flex;
        flex.Min = new Vector2(minX ?? flex.Min.X, minY ?? flex.Min.Y);
        flex.Max = new Vector2(maxX ?? flex.Max.X, maxY ?? flex.Max.Y);
        if (_entity.HasComponent<UiContainer>())
            _entity.AddTag<LayoutDirty>();
        return Touch(layout: true);
    }

    /// <summary>The node's flex, added on first use with its current size as what it asks for.</summary>
    private ref UiFlex Flex
    {
        get
        {
            if (!_entity.HasComponent<UiFlex>())
                _entity.AddComponent(new UiFlex(_entity.GetComponent<Rectangle>().Bounds.Size));
            return ref _entity.GetComponent<UiFlex>();
        }
    }
}
