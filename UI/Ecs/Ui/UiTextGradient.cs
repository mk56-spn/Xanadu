using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A material that tints an entity's white text along a line, in the entity's
/// local space. Attach it with <see cref="UiMaterial"/> to an entity with a
/// transparent fill, so only the glyphs are recoloured. The text colour still
/// supplies the alpha, so fades keep working.
/// </summary>
public static class UiTextGradient
{
    private const string Code = """
        shader_type canvas_item;
        uniform vec4 color_from : source_color = vec4(1.0);
        uniform vec4 color_to : source_color = vec4(1.0);
        uniform vec2 point_from = vec2(0.0);
        uniform vec2 point_to = vec2(1.0, 0.0);
        varying vec2 local;

        void vertex() {
            local = VERTEX;
        }

        void fragment() {
            vec2 line = point_to - point_from;
            float t = clamp(dot(local - point_from, line) / dot(line, line), 0.0, 1.0);
            vec4 tint = mix(color_from, color_to, t);
            COLOR = vec4(tint.rgb * COLOR.rgb, tint.a * COLOR.a);
        }
        """;

    private static Shader? _shader;

    /// <summary>
    /// Blends from <paramref name="from"/> at <paramref name="start"/> to
    /// <paramref name="to"/> at <paramref name="end"/>, clamped beyond either end.
    /// </summary>
    public static ShaderMaterial Create(Color from, Color to, Vector2 start, Vector2 end)
    {
        _shader ??= new Shader { Code = Code };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("color_from", from);
        material.SetShaderParameter("color_to", to);
        material.SetShaderParameter("point_from", start);
        material.SetShaderParameter("point_to", end);
        return material;
    }
}
