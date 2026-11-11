using Xanadu.Layering;

namespace Xanadu.Animation;

/// <summary>
/// The animator's draw order (Docs/render-architecture.md step 12), expressed in
/// the shared <see cref="DrawOrder"/>: an attachment's layer and list index, then
/// cloth, chains and effects after it on the same layer. Every item an animator
/// draws gets its draw index from here, so both the layer panel's rows and the
/// canvas agree on the order.
/// </summary>
public static class AnimatorDrawOrder
{
    /// <summary>Room for attachments on one layer before cloth starts.</summary>
    public const int ClothBand = 10_000;

    /// <summary>Room after cloth before chains start.</summary>
    public const int ChainBand = 20_000;

    /// <summary>Room after chains before effects start.</summary>
    public const int EffectBand = 30_000;

    /// <summary>An attachment's order within its layer: its place in the attachment list.</summary>
    public static int Attachment(int index) => System.Math.Clamp(index, 0, ClothBand - 1);

    /// <summary>Cloth's order on a layer: after every attachment, in cloth-list order.</summary>
    public static int Cloth(int index) => ClothBand + System.Math.Clamp(index, 0, ClothBand - 1);

    /// <summary>A chain segment's order: after cloth, chain by chain and segment by segment.</summary>
    public static int Chain(int chain, int segment) =>
        ChainBand + System.Math.Clamp(chain * 100 + segment, 0, ClothBand - 1);

    /// <summary>An effect primitive's order: after chains, effect by effect and primitive by primitive.</summary>
    public static int Effect(int effect, int primitive) =>
        EffectBand + System.Math.Clamp(effect * 100 + primitive, 0, ClothBand - 1);

    /// <summary>
    /// The layer-panel row's slot: on one layer, cloth draws over attachments and
    /// later list entries over earlier ones. The canvas order above has the same
    /// result, so a move in the panel moves the drawing.
    /// </summary>
    public static DrawSlot Slot(int layer, bool cloth, int index) =>
        new(layer, cloth ? 1 : 0, index);

    /// <summary>The single draw index for everything kept at one z (a CanvasGroup child).</summary>
    public static int Banded(int layer, int order) => DrawOrder.Banded(layer, order);
}
