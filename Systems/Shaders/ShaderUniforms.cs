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
