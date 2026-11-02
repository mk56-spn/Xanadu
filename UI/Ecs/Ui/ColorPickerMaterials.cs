using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// The materials a colour picker's square and hue bar draw with. The shaders live
/// here rather than in a project's assets, so every host gets them, whatever its
/// <c>res://</c> holds. Only call these with Godot running.
/// </summary>
public static class ColorPickerMaterials
{
    private const string HsvToRgb = """
        vec3 hsv_to_rgb(vec3 c) {
        	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
        	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
        	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
        }
        """;

    /// <summary>Saturation left to right by value bottom to top, at the <c>hue</c> uniform.</summary>
    public static ShaderMaterial Square() => Create($$"""
        shader_type canvas_item;
        uniform float hue = 0.0;
        {{HsvToRgb}}
        void fragment() {
        	COLOR = vec4(hsv_to_rgb(vec3(hue, UV.x, 1.0 - UV.y)), 1.0);
        }
        """);

    /// <summary>Hue from top, red, to bottom, red again.</summary>
    public static ShaderMaterial Hue() => Create($$"""
        shader_type canvas_item;
        {{HsvToRgb}}
        void fragment() {
        	COLOR = vec4(hsv_to_rgb(vec3(UV.y, 1.0, 1.0)), 1.0);
        }
        """);

    private static ShaderMaterial Create(string code) =>
        new() { Shader = new Shader { Code = code } };
}
