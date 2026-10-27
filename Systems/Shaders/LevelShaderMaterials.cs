using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Shaders;

/// <summary>Turns a level's shader sources into materials, when they are first needed.</summary>
public static class LevelShaderMaterials
{
    /// <summary>
    /// Compiles any slot that has no material yet or whose source changed,
    /// with the level's tuned values applied. Needs the rendering server.
    /// </summary>
    public static void Ensure(LevelShaders shaders)
    {
        foreach (ShaderSlot? slot in shaders.Slots)
        {
            if (slot is null || slot.Source == slot.CompiledSource)
                continue;

            slot.CompiledSource = slot.Source;
            slot.Material = null;
            if (!Xanadu.Levels.ShaderSourceRules.IsAcceptable(slot.Source, out _))
                continue;

            var shader = new Shader { Code = slot.Source };
            var material = new ShaderMaterial { Shader = shader };
            foreach (ShaderUniform uniform in ShaderUniforms.Describe(shader))
            {
                if (slot.Params.TryGetValue(uniform.Name, out float[]? value) &&
                    value.Length == uniform.Channels)
                {
                    ShaderUniforms.Write(material, uniform, value);
                }
            }

            slot.Material = material;
        }
    }

    public static Rid MaterialOf(LevelShaders shaders, int slot) =>
        shaders.Get(slot)?.Material is { } material ? material.GetRid() : default;
}
