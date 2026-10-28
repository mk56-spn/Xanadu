using Godot;

namespace Xanadu.Animation;

/// <summary>Where each piece of a chain's art goes: one stretched copy per segment.</summary>
public static class XafChainArt
{
    /// <summary>
    /// How much longer than its segment each piece is. Pieces longer than the
    /// spacing overlap, so a chain of rhombi reads as one strand, not as beads.
    /// </summary>
    public const float Overlap = 1.5f;

    /// <summary>Share of the chain's width left at the tip; the strand narrows towards it.</summary>
    public const float TipWidth = 0.45f;

    /// <summary>
    /// The draw layer of every chain's art: one above the highest layer of the
    /// package's attachments, so chains always draw over the body whatever the
    /// order the canvas items happened to be made in.
    /// </summary>
    public static int Layer(XafPackage package)
    {
        int highest = 0;
        foreach (XafAttachment attachment in package.Attachments)
            highest = Mathf.Max(highest, attachment.ZOrder);

        return Mathf.Clamp(highest + 1, XafAttachment.MinLayer, XafAttachment.MaxLayer);
    }

    /// <summary>The width of the piece on one segment, tapering from the root to the tip.</summary>
    public static float WidthAt(float width, int segment, int segments) =>
        width * Mathf.Lerp(1f, TipWidth, (segment + 0.5f) / Mathf.Max(1, segments));

    /// <summary>
    /// The transform that puts art centred on its own origin onto the segment
    /// from <paramref name="from"/> to <paramref name="to"/>, long side along the
    /// segment and stretched to the piece's length and width. The art is
    /// <paramref name="artSize"/> pixels with its long side down its Y axis.
    /// </summary>
    public static Transform2D Segment(
        Vector2 from,
        Vector2 to,
        int segment,
        int segments,
        float width,
        Vector2 artSize)
    {
        Vector2 along = to - from;
        float length = along.Length();
        Vector2 direction = length > 0.0001f ? along / length : Vector2.Down;
        float lengthScale = length * Overlap / Mathf.Max(1f, artSize.Y);
        float widthScale = WidthAt(width, segment, segments) / Mathf.Max(1f, artSize.X);

        // X is the perpendicular that keeps the transform a proper rotation, not a mirror.
        var across = new Vector2(direction.Y, -direction.X);
        return new Transform2D(across * widthScale, direction * lengthScale, (from + to) / 2f);
    }
}
