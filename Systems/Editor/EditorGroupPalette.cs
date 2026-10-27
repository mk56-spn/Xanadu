using Godot;

namespace Xanadu.Systems.Editor;

/// <summary>The fixed colours and alpha steps offered by the group colour palette.</summary>
public static class EditorGroupPalette
{
    public static readonly Color[] Swatches =
    {
        new("#ffffff"),
        new("#1e293b"),
        new("#ef4444"),
        new("#fb923c"),
        new("#facc15"),
        new("#22c55e"),
        new("#22d3ee"),
        new("#3b82f6"),
        new("#8b5cf6"),
        new("#ec4899"),
        new("#94a3b8"),
        new("#64748b"),
    };

    public static readonly float[] Alphas = { 0.25f, 0.5f, 0.75f, 1f };

    public static string AlphaLabel(int index) =>
        $"{(int)System.Math.Round(Alphas[index] * 100)}%";

    /// <summary>The swatch colour with <paramref name="alpha"/> kept from the effect.</summary>
    public static Color WithAlpha(Color colour, float alpha) =>
        new(colour.R, colour.G, colour.B, alpha);
}
