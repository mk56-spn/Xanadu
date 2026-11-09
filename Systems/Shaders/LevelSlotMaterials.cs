using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// A level's shader slots as engine-free materials (docs/moonworks-port.md
/// step 5): one <see cref="CanvasMaterial"/> per slot and blend, made from the
/// slot's Slang source, carrying the slot's tuned values. The host compiles and
/// caches the effect by its source.
/// </summary>
public static class LevelSlotMaterials
{
    /// <summary>The material for a slot drawn with a blend, or null when the slot is empty or its source unusable.</summary>
    public static CanvasMaterial? MaterialOf(LevelShaders shaders, int slot, BlendMode blend = BlendMode.Mix)
    {
        if (shaders.Get(slot) is not { } shader)
            return null;

        if (shader.MaterialSource != shader.Source)
        {
            shader.Materials.Clear();
            shader.MaterialSource = shader.Source;
        }

        if (!shader.Materials.TryGetValue(blend, out CanvasMaterial? material))
        {
            if (!ShaderSourceRules.IsAcceptable(shader.Source, out _))
                return null;

            material = CanvasMaterial.FromSource(Name(slot, shader), shader.Source, blend);
            shader.Materials[blend] = material;

            // The slot's tuned values, set once as Godot's materials were: the F2
            // tuner adjusts the live material afterwards without saving.
            foreach ((string name, float[] value) in shader.Params)
                material.Set(name, new Vector4(At(value, 0), At(value, 1), At(value, 2), At(value, 3)));
        }

        return material;
    }

    private static string Name(int slot, ShaderSlot shader) =>
        shader.Name.Length > 0 ? $"slot {slot} ({shader.Name})" : $"slot {slot}";

    private static float At(float[] value, int index) => index < value.Length ? value[index] : 0f;
}
