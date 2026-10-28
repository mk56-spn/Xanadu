using Godot;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Two separable gaussian passes over a coverage mask held in a drawable
/// texture, ending in a tinted glow. Shared by every glow: whatever fills the
/// mask, text or a shape, the blur is the same.
/// </summary>
internal sealed class GodotBlur
{
    private const int Taps = 16;

    // One axis of a separable gaussian. Taps are dense so the falloff is
    // smooth rather than showing stamped copies of the source.
    private const string Code = """
        shader_type texture_blit;
        render_mode blend_disabled;
        uniform sampler2D source_texture0 : hint_blit_source0, filter_linear, repeat_disable;
        uniform vec2 texel = vec2(0.01);
        uniform vec2 direction = vec2(1.0, 0.0);
        uniform float step_px = 1.5;
        uniform float intensity = 1.0;
        uniform vec4 glow_color : source_color = vec4(1.0);
        uniform bool final_pass = false;

        void blit() {
            vec2 step_uv = direction * step_px * texel;
            float sum = 0.0;
            float total = 0.0;
            for (int i = -16; i <= 16; i++) {
                float f = float(i) / 5.0;
                float w = exp(-0.5 * f * f);
                sum += texture(source_texture0, UV + step_uv * float(i)).a * w;
                total += w;
            }
            float a = sum / total;
            if (final_pass) {
                COLOR0 = vec4(glow_color.rgb, clamp(a * intensity, 0.0, 1.0) * glow_color.a);
            } else {
                COLOR0 = vec4(1.0, 1.0, 1.0, a);
            }
        }
        """;

    private static Shader? _shader;

    private static Shader ShaderResource => _shader ??= new Shader { Code = Code };

    private readonly DrawableTexture2D _horizontal = new();
    private readonly ShaderMaterial _horizontalMaterial = new() { Shader = ShaderResource };
    private readonly ShaderMaterial _verticalMaterial = new() { Shader = ShaderResource };

    /// <summary>The finished glow; its RID can change on every <see cref="Run"/>.</summary>
    public DrawableTexture2D Result { get; } = new();

    /// <summary>How far past its source the blur reaches, for sizing the mask.</summary>
    public static float Padding(float radius) => Mathf.Ceil(Taps * Step(radius)) + 2;

    private static float Step(float radius) => Mathf.Max(1f, radius / 8f);

    /// <summary>Blurs <paramref name="mask"/> into <see cref="Result"/>.</summary>
    public void Run(
        DrawableTexture2D mask,
        Vector2I size,
        float radius,
        Color color,
        float intensity)
    {
        float step = Step(radius);
        var area = new Rect2I(Vector2I.Zero, size);

        SetParams(_horizontalMaterial, new Vector2(1, 0), step, color, intensity, false, size);
        _horizontal.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        _horizontal.BlitRect(area, mask, null, 0, _horizontalMaterial);

        SetParams(_verticalMaterial, new Vector2(0, 1), step, color, intensity, true, size);
        Result.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        Result.BlitRect(area, _horizontal, null, 0, _verticalMaterial);
    }

    private static void SetParams(
        ShaderMaterial material,
        Vector2 direction,
        float step,
        Color color,
        float intensity,
        bool final,
        Vector2I size)
    {
        material.SetShaderParameter("texel", new Vector2(1f / size.X, 1f / size.Y));
        material.SetShaderParameter("direction", direction);
        material.SetShaderParameter("step_px", step);
        material.SetShaderParameter("intensity", intensity);
        material.SetShaderParameter("glow_color", color);
        material.SetShaderParameter("final_pass", final);
    }
}
