// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XanaduProject.Factories.ShaderFactoryHelpers;

namespace XanaduProject.Factories
{
    public class ShaderFactory
    {
        private readonly List<CodeSegment> segments = new();
        private readonly List<RenderMode> renderModes = new();

        public ShaderFactory Add(CodeSegment segment)
        {
            segments.Add(segment);
            return this;
        }

        public ShaderFactory Add(RenderMode renderMode)
        {
            renderModes.Add(renderMode);
            return this;
        }

        private string GenerateCacheKey()
        {
            var keyBuilder = new StringBuilder();

            foreach (var mode in renderModes.Distinct().OrderBy(m => m))
            {
                keyBuilder.Append($"render_mode:{mode};");
            }

            var allUniforms = segments.SelectMany(s => s.Uniforms).Distinct().OrderBy(u => u.Name);
            foreach (var uniform in allUniforms)
            {
                keyBuilder.Append($"uniform:{uniform.Type},{uniform.Name},{uniform.DefaultValue},{uniform.Hint?.ToShaderString()};");
            }

            var allVaryings = segments.OfType<VertexPartial>().SelectMany(s => s.VaryingsOut)
                .Concat(segments.OfType<FragmentPartial>().SelectMany(s => s.VaryingsIn))
                .Distinct().OrderBy(v => v.Name);

            foreach (var varying in allVaryings)
            {
                keyBuilder.Append($"varying:{varying.Type},{varying.Name};");
            }

            foreach (var segment in segments.OrderBy(s => s.Code))
            {
                keyBuilder.Append($"segment:{segment.GetType().Name},{segment.Code};");
            }

            return keyBuilder.ToString();
        }

        public ShaderMaterial Build()
        {
            var key = GenerateCacheKey();
            return ShaderCache.Instance.GetOrCreate(key, BuildInternal);
        }

        private ShaderMaterial BuildInternal()
        {
            var uniforms = segments.SelectMany(s => s.Uniforms).Distinct().ToList();

            var vertexVaryings = segments.OfType<VertexPartial>().SelectMany(p => p.VaryingsOut).ToList();
            var fragmentVaryings = segments.OfType<FragmentPartial>().SelectMany(p => p.VaryingsIn).ToList();

            var missingVaryings = fragmentVaryings.Where(fv => !vertexVaryings.Any(vv => vv.Name == fv.Name && vv.Type == fv.Type)).ToList();
            if (missingVaryings.Any())
            {
                string missingStr = string.Join(", ", missingVaryings.Select(v => $"{v.Type} {v.Name}"));
                throw new InvalidOperationException($"Fragment shader requires varyings that are not supplied by any vertex shader: {missingStr}");
            }

            var allVaryings = vertexVaryings.Concat(fragmentVaryings).Distinct().ToList();

            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("shader_type canvas_item;");

            foreach (var renderMode in renderModes.Distinct())
            {
                codeBuilder.AppendLine($"render_mode {ToShaderString(renderMode)};");
            }

            if (uniforms.Any())
            {
                codeBuilder.AppendLine();
                foreach (var uniform in uniforms)
                {
                    var uniformBuilder = new StringBuilder();
                    uniformBuilder.Append($"uniform {toShaderString(uniform.Type)} {uniform.Name}");
                    if (uniform.Hint != null)
                    {
                        uniformBuilder.Append($": {uniform.Hint.ToShaderString()}");
                    }

                    if (!string.IsNullOrEmpty(uniform.DefaultValue))
                    {
                        uniformBuilder.Append($" = {uniform.DefaultValue}");
                    }

                    uniformBuilder.Append(";");
                    codeBuilder.AppendLine(uniformBuilder.ToString());
                }
            }

            if (allVaryings.Any())
            {
                codeBuilder.AppendLine();
                foreach (var varying in allVaryings)
                {
                    codeBuilder.AppendLine($"varying {toShaderString(varying.Type)} {varying.Name};");
                }
            }

            var globalCodePartials = segments.OfType<GlobalCode>().ToList();
            if (globalCodePartials.Any())
            {
                codeBuilder.AppendLine();
                foreach (var partial in globalCodePartials)
                {
                    codeBuilder.AppendLine(partial.Code);
                }
            }

            var vertexPartials = segments.OfType<VertexPartial>().ToList();
            if (vertexPartials.Any())
            {
                codeBuilder.AppendLine();
                codeBuilder.AppendLine("void vertex() {");
                foreach (var partial in vertexPartials)
                {
                    codeBuilder.AppendLine(partial.Code);
                }

                codeBuilder.AppendLine("}");
            }

            var fragmentPartials = segments.OfType<FragmentPartial>().ToList();
            if (fragmentPartials.Any())
            {
                codeBuilder.AppendLine();
                codeBuilder.AppendLine("void fragment() {");
                foreach (var partial in fragmentPartials)
                {
                    codeBuilder.AppendLine(partial.Code);
                }

                codeBuilder.AppendLine("}");
            }

            return new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = codeBuilder.ToString()
                }
            };
        }

        private static string ToShaderString(RenderMode mode)
        {
            return mode switch
            {
                RenderMode.SkipVertexTransform => "skip_vertex_transform",
                RenderMode.Unshaded => "unshaded",
                RenderMode.LightOnly => "light_only",
                RenderMode.WorldVertexCoords => "world_vertex_coords",
                RenderMode.BlendMix => "blend_mix",
                RenderMode.BlendAdd => "blend_add",
                RenderMode.BlendSub => "blend_sub",
                RenderMode.BlendMul => "blend_mul",
                RenderMode.BlendPremulAlpha => "blend_premul_alpha",
                RenderMode.BlendDisabled => "blend_disabled",
                _ => throw new System.ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        private static string toShaderString(ShaderValueType type)
        {
            return type switch
            {
                ShaderValueType.Float => "float",
                ShaderValueType.Vec2 => "vec2",
                ShaderValueType.Vec3 => "vec3",
                ShaderValueType.Vec4 => "vec4",
                ShaderValueType.IVec2 => "ivec2",
                ShaderValueType.IVec3 => "ivec3",
                ShaderValueType.IVec4 => "ivec4",
                ShaderValueType.UVec2 => "uvec2",
                ShaderValueType.UVec3 => "uvec3",
                ShaderValueType.UVec4 => "uvec4",
                ShaderValueType.Bool => "bool",
                ShaderValueType.BVec2 => "bvec2",
                ShaderValueType.BVec3 => "bvec3",
                ShaderValueType.BVec4 => "bvec4",
                ShaderValueType.Mat2 => "mat2",
                ShaderValueType.Mat3 => "mat3",
                ShaderValueType.Mat4 => "mat4",
                ShaderValueType.Sampler2D => "sampler2D",
                ShaderValueType.Sampler2DArray => "sampler2DArray",
                ShaderValueType.SamplerCube => "samplerCube",
                ShaderValueType.SamplerCubeArray => "samplerCubeArray",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }

    public enum RenderMode
    {
        SkipVertexTransform,
        Unshaded,
        LightOnly,
        WorldVertexCoords,
        BlendMix,
        BlendAdd,
        BlendSub,
        BlendMul,
        BlendPremulAlpha,
        BlendDisabled
    }

    public enum ShaderValueType
    {
        Float,
        Vec2,
        Vec3,
        Vec4,
        IVec2,
        IVec3,
        IVec4,
        UVec2,
        UVec3,
        UVec4,
        Bool,
        BVec2,
        BVec3,
        BVec4,
        Mat2,
        Mat3,
        Mat4,
        Sampler2D,
        Sampler2DArray,
        SamplerCube,
        SamplerCubeArray
    }
}
