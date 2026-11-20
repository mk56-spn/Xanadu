using Xanadu.Core.Maths;
using Xanadu.Model.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Logic.Profiles;

/// <summary>A player's tint and the gradient their art derives from it.</summary>
public static class ProfileTint
{
    // Chosen so the default tint reproduces the original blue art.
    private const float SaturationBoost = 1.62f;
    private const float TopValue = 0.2f;
    private const float BottomValue = 0.45f;

    /// <summary>The profile's tint, or the default one if there is no profile or its hex is bad.</summary>
    public static Color Of(UserProfile? profile) =>
        SelfModulate.TryParse(profile?.NoteTint ?? ProfileStore.DefaultNoteTint, out Color tint)
            ? tint
            : new Color(ProfileStore.DefaultNoteTint);

    /// <summary>The two ends of the gradient behind notes of this tint: its own hue, richer and darker.</summary>
    public static (Color Top, Color Bottom) Gradient(Color tint)
    {
        float saturation = Math.Min(tint.S * SaturationBoost, 1f);
        return (
            Color.FromHsv(tint.H, saturation, tint.V * TopValue),
            Color.FromHsv(tint.H, saturation, tint.V * BottomValue));
    }

    /// <summary>Retints art that is already on screen.</summary>
    public static void Apply(CanvasMaterial material, Color tint)
    {
        (Color top, Color bottom) = Gradient(tint);
        material.Set("note_color", tint);
        material.Set("top_color", top);
        material.Set("bottom_color", bottom);
    }
}
