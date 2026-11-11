using System.Collections.Generic;

namespace Xanadu.Systems.Shaders;

public enum UniformKind
{
    Float,
    Int,
    Vector2,
    Vector3,
    Vector4,
    Color,
}

/// <summary>A tunable shader uniform and the slider range used to edit it.</summary>
public sealed record ShaderUniform(string Name, UniformKind Kind, float Min, float Max)
{
    public int Channels => Kind switch
    {
        UniformKind.Vector2 => 2,
        UniformKind.Vector3 => 3,
        UniformKind.Vector4 or UniformKind.Color => 4,
        _ => 1,
    };

    public string ChannelName(int channel) => Channels == 1
        ? Name
        : $"{Name}.{(Kind == UniformKind.Color ? "rgba" : "xyzw")[channel]}";
}

/// <summary>Reads and writes shader uniforms as flat float arrays.</summary>
public static class ShaderUniforms
{
    /// <summary>Slider range for a uniform with no hint, around its current value.</summary>
    public static (float Min, float Max) GuessRange(UniformKind kind, float reference) =>
        Xanadu.Rendering.UniformRanges.Guess(kind == UniformKind.Color, reference);

    /// <summary>
    /// The tunable values of an engine-free material, from the host's
    /// reflection (Docs/host.md §5.5). A four-channel value whose
    /// name says colour is edited as a colour, standing in for Godot's
    /// <c>source_color</c> hint.
    /// </summary>
    public static List<ShaderUniform> Describe(Xanadu.Systems.Canvas.CanvasMaterial material)
    {
        var uniforms = new List<ShaderUniform>();
        if (HostPlatform.DescribeMaterial?.Invoke(material) is not { } described)
            return uniforms;

        foreach (Xanadu.Systems.Canvas.MaterialParam param in described)
        {
            UniformKind kind = param.IsInt ? UniformKind.Int : param.Channels switch
            {
                2 => UniformKind.Vector2,
                3 => UniformKind.Vector3,
                4 when IsColourName(param.Name) => UniformKind.Color,
                4 => UniformKind.Vector4,
                _ => UniformKind.Float,
            };
            float reference = material.Get(param.Name, param.Default).X;
            (float min, float max) = GuessRange(kind, reference);
            uniforms.Add(new ShaderUniform(param.Name, kind, min, max));
        }

        return uniforms;
    }

    private static bool IsColourName(string name) => Xanadu.Rendering.UniformRanges.IsColourName(name);

    /// <summary>The value an engine-free material's shader declares for a value.</summary>
    public static float[] Default(Xanadu.Systems.Canvas.CanvasMaterial material, ShaderUniform uniform)
    {
        Vector4 value = default;
        if (HostPlatform.DescribeMaterial?.Invoke(material) is { } described)
            foreach (Xanadu.Systems.Canvas.MaterialParam param in described)
                if (param.Name == uniform.Name)
                    value = param.Default;
        return Channels(value, uniform.Channels);
    }

    /// <summary>The material's value, or the shader's default when it sets none.</summary>
    public static float[] Read(Xanadu.Systems.Canvas.CanvasMaterial material, ShaderUniform uniform) =>
        material.Values.TryGetValue(uniform.Name, out Vector4 value)
            ? Channels(value, uniform.Channels)
            : Default(material, uniform);

    public static void Write(Xanadu.Systems.Canvas.CanvasMaterial material, ShaderUniform uniform, float[] v) =>
        material.Set(uniform.Name, new Vector4(
            v.Length > 0 ? (uniform.Kind == UniformKind.Int ? Mathf.Round(v[0]) : v[0]) : 0,
            v.Length > 1 ? v[1] : 0,
            v.Length > 2 ? v[2] : 0,
            v.Length > 3 ? v[3] : 0));

    private static float[] Channels(Vector4 value, int count) =>
        new[] { value.X, value.Y, value.Z, value.W }[..count];
}
