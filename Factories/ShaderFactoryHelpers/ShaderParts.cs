// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace XanaduProject.Factories.ShaderFactoryHelpers
{
    public interface IUniform
    {
        ShaderValueType Type { get; }
        string Name { get; }
        string? DefaultValue { get; }
        UniformHint? Hint { get; }
    }

    public readonly struct Uniform(ShaderValueType type, string name, string? defaultValue = null, UniformHint? hint = null) : IUniform
    {
        public ShaderValueType Type { get; } = type;
        public string Name { get; } = name;
        public string? DefaultValue { get; } = defaultValue;
        public UniformHint? Hint { get; } = hint;
    }

    public readonly struct Uniform<T>(string name, T? defaultValue = default, UniformHint? hint = null)
        : IUniform
    {
        public ShaderValueType Type { get; } = GetShaderValueType();
        public string Name { get; } = name;
        public string? DefaultValue { get; } = ToShaderString(defaultValue);
        public UniformHint? Hint { get; } = hint;

        private static ShaderValueType GetShaderValueType()
        {
            if (typeof(T) == typeof(float)) return ShaderValueType.Float;
            if (typeof(T) == typeof(Vector2)) return ShaderValueType.Vec2;
            if (typeof(T) == typeof(Vector3)) return ShaderValueType.Vec3;
            if (typeof(T) == typeof(Vector4)) return ShaderValueType.Vec4;
            if (typeof(T) == typeof(bool)) return ShaderValueType.Bool;
            if (typeof(T) == typeof(Texture2D)) return ShaderValueType.Sampler2D;
            throw new System.ArgumentException($"Unsupported uniform type: {typeof(T).Name}");
        }

        private static string? ToShaderString(T? value)
        {
            if (value == null) return null;

            return value switch
            {
                float f => f.ToString(CultureInfo.InvariantCulture),
                Vector2 v => $"vec2({v.X.ToString(CultureInfo.InvariantCulture)}, {v.Y.ToString(CultureInfo.InvariantCulture)})",
                Vector3 v => $"vec3({v.X.ToString(CultureInfo.InvariantCulture)}, {v.Y.ToString(CultureInfo.InvariantCulture)}, {v.Z.ToString(CultureInfo.InvariantCulture)})",
                Vector4 v => $"vec4({v.X.ToString(CultureInfo.InvariantCulture)}, {v.Y.ToString(CultureInfo.InvariantCulture)}, {v.Z.ToString(CultureInfo.InvariantCulture)}, {v.W.ToString(CultureInfo.InvariantCulture)})",
                bool b => b.ToString().ToLowerInvariant(),
                _ => value.ToString()
            };
        }
    }


    public readonly record struct Varying(ShaderValueType type, string name)
    {
        public ShaderValueType Type { get; } = type;
        public string Name { get; } = name;
    }

    public abstract record CodeSegment
    {
        public string Code { get; } = "";
        public IEnumerable<IUniform> Uniforms { get; } = [];

        protected CodeSegment(string code, IEnumerable<IUniform>? uniforms)
        {
            Code = code;
            Uniforms = uniforms ?? [];
        }
    }

    public record GlobalCode(string Code, IEnumerable<IUniform>? Uniforms = null) : CodeSegment(Code, Uniforms);

    public record VertexPartial : CodeSegment
    {
        public IEnumerable<Varying> VaryingsOut { get; } = [];

        public VertexPartial(string code, IEnumerable<IUniform>? uniforms = null, IEnumerable<Varying>? varyingsOut = null)
            : base(code, uniforms)
        {
            VaryingsOut = varyingsOut ?? [];
        }
    }

    public record FragmentPartial : CodeSegment
    {
        public IEnumerable<Varying> VaryingsIn { get; } = Enumerable.Empty<Varying>();

        public FragmentPartial(string code, IEnumerable<IUniform>? uniforms = null, IEnumerable<Varying>? varyingsIn = null)
            : base(code, uniforms)
        {
            VaryingsIn = varyingsIn ?? Enumerable.Empty<Varying>();
        }
    }
}
