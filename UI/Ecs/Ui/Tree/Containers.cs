using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Tree;

/// <summary>A stack: its settings, then its <see cref="Children"/> in order.</summary>
public abstract class StackSpec : UiSpec
{
    public UiChildren Children { get; init; } = new();

    public float Gap { get; init; }
    public float Pad { get; init; }
    public StackMainAlignment? Main { get; init; }
    public StackCrossAlignment? Cross { get; init; }

    /// <summary>Hides what spills past its box.</summary>
    public bool Clip { get; init; }

    protected override void Built(UiNode node)
    {
        if (Gap > 0)
            node.Gap(Gap);
        if (Pad > 0)
            node.Pad(Pad);
        if (Main is { } main)
            node.Main(main);
        if (Cross is { } cross)
            node.Cross(cross);
        if (Clip)
            node.Clip();

        foreach (UiSpec child in Children)
            node.Add(child);
    }
}

/// <summary>Children stacked top to bottom, sized to them unless told otherwise.</summary>
public sealed class Column : StackSpec
{
    protected override UiNode Create(UiNode parent) => parent.Column();
}

/// <summary>Children side by side, sized to them unless told otherwise.</summary>
public sealed class Row : StackSpec
{
    protected override UiNode Create(UiNode parent) => parent.Row();
}

/// <summary>A themed panel: the panel fill, padding and spacing.</summary>
public sealed class Panel : StackSpec
{
    protected override UiNode Create(UiNode parent) => parent.Panel();
}

/// <summary>Takes the room left in its stack (grow 1 unless given a weight), pushing what follows to the far end.</summary>
public sealed class Spacer : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.Column().Grow(Grow > 0 ? Grow : 1);
}
