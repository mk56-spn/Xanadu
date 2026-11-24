namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// One node of a tree the host keeps on the GPU between frames
/// (Docs/host.md §5.3): where it sits in its parent, how much it fades
/// what it draws and everything under it, and the clip it gives its children.
/// The host works out the world placement itself, so a mount sends a node
/// only when it changes.
/// </summary>
public struct ResidentNode
{
    /// <summary>The parent's slot, or <see cref="ResidentTree.NoParent"/> for one hanging straight under the mount.</summary>
    public int Parent;

    /// <summary>Its place in the parent's space.</summary>
    public Transform2D Local;

    /// <summary>Multiplies its alpha and its subtree's.</summary>
    public float Opacity;

    /// <summary>When set, its children are cut to <see cref="Clip"/>, in its own space.</summary>
    public bool Clips;

    public Rect2 Clip;

    public readonly bool Same(in ResidentNode other) =>
        Parent == other.Parent && Local == other.Local && Opacity == other.Opacity && Clips == other.Clips &&
        (!Clips || Clip == other.Clip);
}

/// <summary>
/// A tree of drawn items the host keeps on the GPU for one mount: a slot per
/// item, each with its <see cref="ResidentNode"/>, and the order they draw in.
/// The mount writes what changed; the host places, orders and draws the lot
/// with no walk on the CPU.
/// </summary>
public interface IResidentTree
{
    /// <summary>A new slot drawing <paramref name="item"/>.</summary>
    int Add(Handle item);

    void Remove(int slot);

    void Set(int slot, in ResidentNode node);

    /// <summary>
    /// The slots that draw, in walk order (a parent before its subtree), each
    /// with its z within the mount: they draw by z, then in this order.
    /// </summary>
    void SetOrder(ReadOnlySpan<int> slots, ReadOnlySpan<int> z);
}

/// <summary>Shared values for <see cref="IResidentTree"/>.</summary>
public static class ResidentTree
{
    public const int NoParent = -1;
}
