using System.Collections.Generic;
using Godot;
using Xanadu.Rendering;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// A built-in canvas material for each blend mode, used by plain (unshaded)
/// draws (docs/render-architecture.md D17). Made once per mode and kept for the
/// process's lifetime, like the common shader materials.
/// </summary>
public static class PlainBlendMaterials
{
    private static readonly Dictionary<BlendMode, CanvasItemMaterial> Cache = new();

    /// <summary>The material plain draws use for a blend; <see cref="BlendMode.Mix"/> is no material.</summary>
    public static Rid MaterialOf(BlendMode blend)
    {
        if (blend == BlendMode.Mix)
            return default;

        if (!Cache.TryGetValue(blend, out CanvasItemMaterial? material))
        {
            material = new CanvasItemMaterial { BlendMode = ToGodot(blend) };
            Cache[blend] = material;
        }

        return material.GetRid();
    }

    /// <summary>Whether a mode's material is cached and its Godot object is still alive.</summary>
    public static bool IsLive(BlendMode blend) =>
        Cache.TryGetValue(blend, out CanvasItemMaterial? material) && GodotObject.IsInstanceValid(material);

    /// <summary>The Godot blend a mode means.</summary>
    public static CanvasItemMaterial.BlendModeEnum ToGodot(BlendMode blend) => blend switch
    {
        BlendMode.Add => CanvasItemMaterial.BlendModeEnum.Add,
        BlendMode.Sub => CanvasItemMaterial.BlendModeEnum.Sub,
        BlendMode.Mul => CanvasItemMaterial.BlendModeEnum.Mul,
        BlendMode.Premultiplied => CanvasItemMaterial.BlendModeEnum.PremultAlpha,
        _ => CanvasItemMaterial.BlendModeEnum.Mix,
    };
}
