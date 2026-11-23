using Xanadu.Core.Maths;
using Xanadu.Logic.Profiles;
using Xanadu.Screens.SongSelect;
using Xanadu.Storage.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Screens.Profiles;

/// <summary>A player's picture: the notes shader, with its gradient derived from their tint.</summary>
public static class ProfileArt
{
    private const float DefaultZoom = 4.5f;

    /// <summary>The name a player's art goes by in the shader tuner.</summary>
    public static string Instance(UserProfile? profile) => $"profile {profile?.Id ?? "none"}";

    /// <summary>The art's material, or null when assets are not available.</summary>
    public static CanvasMaterial? Material(bool loadAssets, Color tint, string instance, float zoom = DefaultZoom)
    {
        (Color top, Color bottom) = ProfileTint.Gradient(tint);
        return SongSelectScene.NoteMaterial(loadAssets, aspect: 1, zoom, tint, top, bottom, instance);
    }
}
