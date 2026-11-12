using System;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Layout;

/// <summary>
/// Sizing a stack's children: each starts from its natural size, growing
/// children share the main-axis room left over by weight (a child that reaches
/// its max stops and the rest share what it left), stretching ones fill the
/// cross axis, and every <see cref="UiFlex"/> child is held to its min and max.
/// The stack never shrinks a child below its natural size; one that is too
/// full overflows, as before.
/// </summary>
public sealed partial class ContainerLayoutSystem
{
    /// <summary>Fills <see cref="_children"/> and <see cref="_sizes"/> with the stack's children and their laid-out sizes.</summary>
    private void SizeChildren(Entity entity, UiContainer container, Rectangle rectangle, CommandBuffer buffer)
    {
        _children.Clear();
        _sizes.Clear();

        bool horizontal = container.Direction == StackDirection.Horizontal;
        Vector2 box = rectangle.Bounds.Size - new Vector2(container.Padding, container.Padding) * 2;
        float availableMain = horizontal ? box.X : box.Y;
        float availableCross = horizontal ? box.Y : box.X;
        bool stretchAll = container.Cross == StackCrossAlignment.Stretch;

        float used = 0;
        float weights = 0;
        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 size;
            if (child.TryGetComponent(out UiFlex flex))
            {
                size = flex.Natural;
                if (flex.Stretch || stretchAll)
                    SetCross(ref size, horizontal, availableCross);
                size = flex.Clamp(size);
                weights += flex.Grow > 0 ? flex.Grow : 0;
            }
            else
            {
                // A stretched child needs to remember the size it asked for; it
                // keeps today's rectangle, from next frame on.
                size = child.GetComponent<Rectangle>().Bounds.Size;
                if (stretchAll)
                {
                    buffer.AddComponent(child.Id, new UiFlex(size));
                    SetCross(ref size, horizontal, availableCross);
                }
            }

            _children.Add(child);
            _sizes.Add(size);
            Vector2 slot = SlotSize(child, size, container.Direction);
            used += horizontal ? slot.X : slot.Y;
        }

        if (_children.Count > 1)
            used += container.Spacing * (_children.Count - 1);

        if (weights > 0 && availableMain > used)
            Grow(horizontal, availableMain - used);
    }

    /// <summary>Shares <paramref name="free"/> main-axis room between the growing children, by weight.</summary>
    private void Grow(bool horizontal, float free)
    {
        // Each pass either places everyone or caps at least one child at its max.
        Span<bool> capped = stackalloc bool[_children.Count];
        while (free > 0.001f)
        {
            float weights = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                if (!capped[i] && GrowOf(_children[i]) is var grow && grow > 0)
                    weights += grow;
            }

            if (weights <= 0)
                return;

            float share = free / weights;
            bool cappedAny = false;
            for (int i = 0; i < _children.Count; i++)
            {
                float grow = GrowOf(_children[i]);
                if (capped[i] || grow <= 0)
                    continue;

                UiFlex flex = _children[i].GetComponent<UiFlex>();
                float max = horizontal ? flex.Max.X : flex.Max.Y;
                float current = horizontal ? _sizes[i].X : _sizes[i].Y;
                if (max > 0 && current + share * grow >= max)
                {
                    capped[i] = true;
                    cappedAny = true;
                    free -= Mathf.Max(0, max - current);
                    _sizes[i] = WithMain(_sizes[i], horizontal, Mathf.Max(current, max));
                }
            }

            if (cappedAny)
                continue;

            for (int i = 0; i < _children.Count; i++)
            {
                float grow = GrowOf(_children[i]);
                if (capped[i] || grow <= 0)
                    continue;

                float current = horizontal ? _sizes[i].X : _sizes[i].Y;
                _sizes[i] = WithMain(_sizes[i], horizontal, current + share * grow);
            }

            return;
        }
    }

    private static float GrowOf(Entity child) =>
        child.TryGetComponent(out UiFlex flex) && flex.Grow > 0 ? flex.Grow : 0;

    /// <summary>The size a child asks for: its natural size held to its min and max, or its rectangle when it has no flex.</summary>
    private static Vector2 NaturalSize(Entity child) =>
        child.TryGetComponent(out UiFlex flex)
            ? flex.Clamp(flex.Natural)
            : child.GetComponent<Rectangle>().Bounds.Size;

    private static void SetCross(ref Vector2 size, bool horizontal, float cross)
    {
        if (horizontal)
            size.Y = cross;
        else
            size.X = cross;
    }

    private static Vector2 WithMain(Vector2 size, bool horizontal, float main) =>
        horizontal ? new Vector2(main, size.Y) : new Vector2(size.X, main);
}
