namespace Xanadu.Animation;

/// <summary>
/// Draw index for art kept at one z: every layer gets its own band, so a layer always
/// draws over the one below, and the order inside the layer still applies.
/// </summary>
public static class ArtDrawOrder
{
    /// <summary>Wider than any order within a layer.</summary>
    private const int LayerSpan = 100_000;

    public static int Of(int layer, int order) =>
        (layer - XafAttachment.MinLayer) * LayerSpan + order;
}
