using System;

namespace Xanadu.Layering;

/// <summary>
/// Where one thing draws: its layer, then its z inside the layer, then its place
/// in its list. Higher draws in front on every count. Shared by the animator and
/// levels (Docs/render-architecture.md).
/// </summary>
public readonly record struct DrawSlot(int Layer, int Z, int Index) : IComparable<DrawSlot>
{
    /// <summary>Back to front: a slot that compares greater draws over one that compares less.</summary>
    public int CompareTo(DrawSlot other)
    {
        int layer = Layer.CompareTo(other.Layer);
        if (layer != 0)
            return layer;
        int z = Z.CompareTo(other.Z);
        return z != 0 ? z : Index.CompareTo(other.Index);
    }
}

/// <summary>The layer range and the draw index both tools give canvas items.</summary>
public static class DrawOrder
{
    /// <summary>The lowest and highest layer. Levels skip 0, where the player wedge sits.</summary>
    public const int MinLayer = -200;
    public const int MaxLayer = 200;

    /// <summary>Wider than any order within a layer.</summary>
    private const int LayerSpan = 100_000;

    public static int ClampLayer(int layer) => Math.Clamp(layer, MinLayer, MaxLayer);

    /// <summary>
    /// Draw index for things kept at one z index: every layer gets its own band,
    /// so a layer always draws over the one below, and the order inside the layer
    /// still applies. <paramref name="order"/> must stay below 100 000.
    /// </summary>
    public static int Banded(int layer, int order) =>
        (ClampLayer(layer) - MinLayer) * LayerSpan + order;
}
