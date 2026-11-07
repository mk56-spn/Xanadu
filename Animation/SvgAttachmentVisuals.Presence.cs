using Godot;

namespace Xanadu.Animation;

public sealed partial class SvgAttachmentVisuals
{
    /// <summary>
    /// Lights each canvas item through its modulate, which nothing else on an
    /// attachment, chain or cloth uses (their colour is the self modulate), and
    /// keeps an effect's pose alpha. Opacity only goes to nothing, so overlapping
    /// parts never show through each other.
    /// </summary>
    public void ApplyPresence(float light, bool shown)
    {
        var modulate = new Color(light, light, light, shown ? 1f : 0f);
        foreach (Visual visual in _visuals)
            RenderingServer.CanvasItemSetModulate(visual.Item, modulate);

        foreach (ChainVisual chain in _chains)
        {
            foreach (Rid item in chain.Items)
                RenderingServer.CanvasItemSetModulate(item, modulate);
        }

        foreach (ClothVisual cloth in _cloths)
            RenderingServer.CanvasItemSetModulate(cloth.Item, modulate);

        foreach (EffectVisual effect in _effects)
            RenderingServer.CanvasItemSetModulate(effect.Item, modulate with { A = shown ? effect.Alpha : 0f });
    }
}
