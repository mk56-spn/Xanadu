namespace Xanadu.Systems.Shaders;

/// <summary>
/// The glow around a circle, worked out per pixel. The circle is drawn as a
/// ring as wide as its glow reaches, whose UV.x runs from -1 on the inside
/// edge to 1 on the outside edge, so it is the pixel's distance from the
/// circle, as a share of the glow's reach. The light falls off from the line,
/// and is added, so overlapping glows brighten rather than cover each other.
/// </summary>
public static class GlowRingShader
{
    /// <summary>The key it is selected and cached by; it ships in code, so no file can go missing.</summary>
    public const string Path = "builtin://glow-ring";

    public const string Code = """
        shader_type canvas_item;
        render_mode blend_add, unshaded;

        void fragment() {
            float distance = clamp(abs(UV.x), 0.0, 1.0);
            float light = 1.0 - distance;
            COLOR = vec4(COLOR.rgb, COLOR.a * light * light);
        }
        """;
}
