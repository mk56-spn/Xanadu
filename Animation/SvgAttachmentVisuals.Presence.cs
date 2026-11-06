using System;
using Godot;

namespace Xanadu.Animation;

public sealed partial class SvgAttachmentVisuals
{
    /// <summary>
    /// Shades each canvas item through its modulate, which nothing else on an
    /// attachment, chain or cloth uses (their colour is the self modulate), and
    /// keeps an effect's pose alpha. Only colour changes, never opacity.
    /// </summary>
    public void ApplyPresence(float shade, ReadOnlySpan<(int Attachment, float Light)> kept)
    {
        var shaded = new Color(shade, shade, shade);
        foreach (Visual visual in _visuals)
        {
            Color modulate = shaded;
            foreach ((int attachment, float light) in kept)
            {
                if (attachment == visual.Index)
                    modulate = new Color(light, light, light);
            }

            RenderingServer.CanvasItemSetModulate(visual.Item, modulate);
        }

        foreach (ChainVisual chain in _chains)
        {
            foreach (Rid item in chain.Items)
                RenderingServer.CanvasItemSetModulate(item, shaded);
        }

        foreach (ClothVisual cloth in _cloths)
            RenderingServer.CanvasItemSetModulate(cloth.Item, shaded);

        foreach (EffectVisual effect in _effects)
            RenderingServer.CanvasItemSetModulate(effect.Item, new Color(shade, shade, shade, effect.Alpha));
    }
}
