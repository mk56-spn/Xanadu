using Godot;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>The fixed colours and alpha steps offered by the group colour palette.</summary>
public static class EditorGroupPalette
{
    public static readonly Color[] Swatches =
    {
        new("#ffffff"),
        Palette.SurfaceAlt,
        Palette.Danger,
        Palette.Warning,
        Palette.Accent,
        Palette.Success,
        Palette.Cyan,
        Palette.PrimarySoft,
        Palette.Violet,
        Palette.Pink,
        Palette.TextDim,
        Palette.BorderSubtle,
    };

    public static readonly float[] Alphas = { 0.25f, 0.5f, 0.75f, 1f };

    public static string AlphaLabel(int index) =>
        $"{(int)System.Math.Round(Alphas[index] * 100)}%";

    /// <summary>The swatch colour with <paramref name="alpha"/> kept from the effect.</summary>
    public static Color WithAlpha(Color colour, float alpha) =>
        Palette.WithAlpha(colour, alpha);
}
