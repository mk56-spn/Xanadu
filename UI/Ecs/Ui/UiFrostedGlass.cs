using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A material that blurs whatever is drawn behind a translucent panel, so
/// text on it stays readable. It samples the screen at a high mip level, the
/// usual cheap blur, and lays the panel's own colour over it. Attach it with
/// <see cref="UiMaterial"/> to a shape or rect with a translucent fill.
/// </summary>
public static class UiFrostedGlass
{
    private const string Code = """
        shader_type canvas_item;
        uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
        uniform float lod = 3.0;

        void fragment() {
            vec3 behind = textureLod(screen_texture, SCREEN_UV, lod).rgb;
            COLOR = vec4(mix(behind, COLOR.rgb, COLOR.a), 1.0);
        }
        """;

    private static Shader? _shader;

    /// <summary>Higher <paramref name="lod"/> blurs more: each step halves the resolution.</summary>
    public static ShaderMaterial Create(float lod = 3f)
    {
        _shader ??= new Shader { Code = Code };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("lod", lod);
        return material;
    }
}
