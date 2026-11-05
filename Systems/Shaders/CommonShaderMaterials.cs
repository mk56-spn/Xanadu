using System.Collections.Generic;
using Godot;
using Xanadu.Rendering;

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

    /// <summary>
    /// The material of a common shader, with a blend. Mix is the shader's own
    /// material; every other mode is one variant, compiled the first time it is
    /// drawn (docs/render-architecture.md D17).
    /// </summary>
    public static Rid MaterialOf(string path, BlendMode blend = BlendMode.Mix)
    {
        string key = blend == BlendMode.Mix ? path : $"{path}#{blend}";
        if (!Cache.TryGetValue(key, out ShaderMaterial? material))
        {
            material = Build(path, blend);
            if (material is not null)
                Cache[key] = material;
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

    private static ShaderMaterial? Build(string path, BlendMode blend)
    {
        // The game's own effect shaders are written in code, not loaded from a file.
        bool inline = path == GlowRingShader.Path;
        if (inline)
        {
            string code = blend == BlendMode.Mix
                ? GlowRingShader.Code
                : ShaderBlendVariants.Inject(GlowRingShader.Code, blend);
            return new ShaderMaterial { Shader = new Shader { Code = code } };
        }

        if (blend == BlendMode.Mix)
            return ShaderMaterials.Load(path);

        if (GD.Load<Shader>(path) is not { } baseShader)
            return null;
        string source = ShaderBlendVariants.Inject(baseShader.Code, blend);
        return new ShaderMaterial { Shader = new Shader { Code = source } };
    }
}
