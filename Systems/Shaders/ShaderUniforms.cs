using System.Collections.Generic;
using Godot;

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
    public static (float Min, float Max) GuessRange(UniformKind kind, float reference)
    {
        if (kind == UniformKind.Color)
            return (0, 1);

        float span = Mathf.Max(1f, Mathf.Abs(reference) * 4f);
        return (reference < 0 ? -span : 0, span);
    }

    /// <summary>
    /// The tunable values of an engine-free material, from the host's
    /// reflection (docs/moonworks-port.md step 5). A four-channel value whose
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

    private static bool IsColourName(string name) =>
        name.Contains("color", System.StringComparison.OrdinalIgnoreCase) ||
        name.Contains("colour", System.StringComparison.OrdinalIgnoreCase) ||
        name.Contains("tint", System.StringComparison.OrdinalIgnoreCase);

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

    public static List<ShaderUniform> Describe(Shader shader)
    {
        var uniforms = new List<ShaderUniform>();
        foreach (Variant entry in shader.GetShaderUniformList())
        {
            var info = entry.AsGodotDictionary();
            string name = info["name"].AsString();
            UniformKind? kind = ((Variant.Type)info["type"].AsInt32()) switch
            {
                Variant.Type.Float => UniformKind.Float,
                Variant.Type.Int => UniformKind.Int,
                Variant.Type.Vector2 => UniformKind.Vector2,
                Variant.Type.Vector3 => UniformKind.Vector3,
                Variant.Type.Vector4 => UniformKind.Vector4,
                Variant.Type.Color => UniformKind.Color,
                _ => null,
            };
            if (kind is null)
                continue;

            var probe = new ShaderUniform(name, kind.Value, 0, 1);
            float[] current = Default(shader, probe);
            (float min, float max) = GuessRange(kind.Value, current[0]);

            // hint_range(min, max, step) arrives as "min,max,step".
            string[] hint = info["hint_string"].AsString().Split(',');
            if (info["hint"].AsInt32() == (int)PropertyHint.Range &&
                hint.Length >= 2 &&
                float.TryParse(hint[0], out float hintMin) &&
                float.TryParse(hint[1], out float hintMax))
            {
                min = hintMin;
                max = hintMax;
            }

            uniforms.Add(probe with { Min = min, Max = max });
        }

        return uniforms;
    }

    /// <summary>The value the shader declares for a uniform.</summary>
    public static float[] Default(Shader shader, ShaderUniform uniform) =>
        Unpack(
            uniform,
            RenderingServer.ShaderGetParameterDefault(shader.GetRid(), uniform.Name));

    public static float[] Read(ShaderMaterial material, ShaderUniform uniform)
    {
        Variant value = material.GetShaderParameter(uniform.Name);
        return value.VariantType == Variant.Type.Nil && material.Shader is { } shader
            ? Default(shader, uniform)
            : Unpack(uniform, value);
    }

    public static void Write(ShaderMaterial material, ShaderUniform uniform, float[] v) =>
        material.SetShaderParameter(uniform.Name, uniform.Kind switch
        {
            UniformKind.Float => v[0],
            UniformKind.Int => Mathf.RoundToInt(v[0]),
            UniformKind.Vector2 => new Vector2(v[0], v[1]),
            UniformKind.Vector3 => new Vector3(v[0], v[1], v[2]),
            UniformKind.Vector4 => new Vector4(v[0], v[1], v[2], v[3]),
            _ => new Color(v[0], v[1], v[2], v[3]),
        });

    private static float[] Unpack(ShaderUniform uniform, Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
            return new float[uniform.Channels];

        return uniform.Kind switch
        {
            UniformKind.Float => [value.AsSingle()],
            UniformKind.Int => [value.AsInt32()],
            UniformKind.Vector2 => value.AsVector2() is var a ? [a.X, a.Y] : [],
            UniformKind.Vector3 => value.AsVector3() is var b ? [b.X, b.Y, b.Z] : [],
            UniformKind.Vector4 => value.AsVector4() is var c ? [c.X, c.Y, c.Z, c.W] : [],
            _ => value.AsColor() is var d ? [d.R, d.G, d.B, d.A] : [],
        };
    }
}
