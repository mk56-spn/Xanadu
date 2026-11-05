using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;

namespace Xanadu.Systems.Shaders;

/// <summary>Turns a level's shader sources into materials, when they are first needed.</summary>
public static class LevelShaderMaterials
{
    /// <summary>
    /// Compiles any slot that has no material yet or whose source changed,
    /// with the level's tuned values applied. Needs the rendering server.
    /// Changing the source also drops the slot's blend variants.
    /// </summary>
    public static void Ensure(LevelShaders shaders)
    {
        foreach (ShaderSlot? slot in shaders.Slots)
        {
            if (slot is null || slot.Source == slot.CompiledSource)
                continue;

            slot.CompiledSource = slot.Source;
            slot.Material = null;
            slot.BlendVariants.Clear();
            if (!Xanadu.Levels.ShaderSourceRules.IsAcceptable(slot.Source, out _))
                continue;

            slot.Material = Build(slot, slot.Source);
        }
    }

    /// <summary>
    /// The material of a slot with a blend. Mix is the slot's own material;
    /// every other mode is one variant, compiled the first time it is drawn
    /// (docs/render-architecture.md D17).
    /// </summary>
    public static Rid MaterialOf(LevelShaders shaders, int slot, BlendMode blend = BlendMode.Mix)
    {
        ShaderSlot? material = shaders.Get(slot);
        if (material?.Material is null)
            return default;
        if (blend == BlendMode.Mix)
            return material.Material.GetRid();

        if (!material.BlendVariants.TryGetValue(blend, out ShaderMaterial? variant))
        {
            variant = Build(material, ShaderBlendVariants.Inject(material.Source, blend));
            material.BlendVariants[blend] = variant;
        }

        return variant.GetRid();
    }

    /// <summary>A material for a slot's source, with the slot's tuned values applied.</summary>
    private static ShaderMaterial Build(ShaderSlot slot, string source)
    {
        var shader = new Shader { Code = source };
        var material = new ShaderMaterial { Shader = shader };
        foreach (ShaderUniform uniform in ShaderUniforms.Describe(shader))
        {
            if (slot.Params.TryGetValue(uniform.Name, out float[]? value) &&
                value.Length == uniform.Channels)
            {
                ShaderUniforms.Write(material, uniform, value);
            }
        }

        return material;
    }
}
