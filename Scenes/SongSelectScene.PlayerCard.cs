using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Systems.Shaders;

namespace Xanadu.Scenes;

/// <summary>The player card: the active profile's name in a black panel.</summary>
public static partial class SongSelectScene
{
    private const string WaveShader = "res://Assets/Shaders/wave_background.gdshader";

    private static readonly Vector2 CardCenter = new(1396.976f, 880.478f);
    private static readonly Vector2 CardSize = new(983.799f, 220.955f);

    private static void BuildPlayerCard(UiBuilder ui, ProfileStore profiles)
    {
        ui.RectEntity(CardCenter, CardSize, Colors.Black);

        string name = profiles.ActiveProfile?.Name ??
            LocalizedText.Get(SongSelectStrings.NoProfile);
        SceneUi.Text(
            ui,
            CardCenter,
            CardSize,
            Fit(name, 18),
            80,
            MapsetColor,
            Transparent);
    }

    /// <summary>Gives the full-screen background a shader, when assets are available.</summary>
    private static void ApplyBackgroundShader(Entity background, bool loadAssets)
    {
        if (loadAssets && ShaderMaterials.Load(WaveShader) is { } material)
            background.AddComponent(new UiMaterial(material));
    }
}
