using Xanadu.Systems.Canvas;
using Xanadu.Ecs;
using Xanadu.Profiles;

namespace Xanadu.Scenes;

/// <summary>A player's picture: the notes shader, with its gradient derived from their tint.</summary>
public static class ProfileArt
{
    private const float DefaultZoom = 4.5f;

    // Chosen so the default tint reproduces the original blue art.
    private const float SaturationBoost = 1.62f;
    private const float TopValue = 0.2f;
    private const float BottomValue = 0.45f;

    /// <summary>The profile's tint, or the default one if there is no profile or its hex is bad.</summary>
    public static Color Tint(UserProfile? profile) =>
        SelfModulate.TryParse(profile?.NoteTint ?? ProfileStore.DefaultNoteTint, out Color tint)
            ? tint
            : new Color(ProfileStore.DefaultNoteTint);

    /// <summary>The two ends of the gradient behind notes of this tint: its own hue, richer and darker.</summary>
    public static (Color Top, Color Bottom) Gradient(Color tint)
    {
        float saturation = Mathf.Min(tint.S * SaturationBoost, 1f);
        return (
            Color.FromHsv(tint.H, saturation, tint.V * TopValue),
            Color.FromHsv(tint.H, saturation, tint.V * BottomValue));
    }

    /// <summary>The name a player's art goes by in the shader tuner.</summary>
    public static string Instance(UserProfile? profile) => $"profile {profile?.Id ?? "none"}";

    /// <summary>The art's material, or null when assets are not available.</summary>
    public static CanvasMaterial? Material(bool loadAssets, Color tint, string instance, float zoom = DefaultZoom)
    {
        (Color top, Color bottom) = Gradient(tint);
        return SongSelectScene.NoteMaterial(loadAssets, aspect: 1, zoom, tint, top, bottom, instance);
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
