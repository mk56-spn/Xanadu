using System.Collections.Generic;
using Godot;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// Materials for the game's common shaders, loaded once per path and cached
/// for the process's lifetime: they ship with the game, so unlike a map's own
/// shaders they never change underneath a running game.
/// </summary>
/// <remarks>
/// The cache holds the <see cref="ShaderMaterial"/> itself, not just its
/// <see cref="Rid"/>. A Godot resource's native side is owned by its C#
/// wrapper; once nothing keeps the wrapper alive, the GC can collect it,
/// which frees the native material and leaves any cached Rid dangling.
/// </remarks>
public static class CommonShaderMaterials
{
    private static readonly Dictionary<string, ShaderMaterial> Cache = new();

    public static Rid MaterialOf(string path)
    {
        if (!Cache.TryGetValue(path, out ShaderMaterial? material))
        {
            material = ShaderMaterials.Load(path);
            if (material is not null)
                Cache[path] = material;
        }

        return material?.GetRid() ?? default;
    }

    /// <summary>
    /// Whether a path's material is cached and the Godot object backing it
    /// has not been collected. <see cref="Rid.IsValid"/> cannot tell this: a
    /// Rid's handle stays non-zero even after the resource behind it is gone.
    /// </summary>
    public static bool IsLive(string path) =>
        Cache.TryGetValue(path, out ShaderMaterial? material) && GodotObject.IsInstanceValid(material);
}
