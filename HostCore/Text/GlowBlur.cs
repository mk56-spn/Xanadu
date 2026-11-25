namespace Xanadu.Host.Text;

/// <summary>
/// The glow behind a shape (<c>HostShapeGlow</c>), done on the CPU and cached: the same
/// separable gaussian as Godot's <c>GodotBlur</c> (33 taps, σ = 5 taps, taps
/// <c>max(1, radius / 8)</c> pixels apart), then alpha scaled by the intensity
/// and clamped. The tint comes from the vertex colour when it is drawn.
/// </summary>
/// <remarks>Text glows are blurred on the GPU instead (<c>KindHalo</c> in primitives.slang).</remarks>
internal static class GlowBlur
{
    private const int Taps = 16;

    private static readonly float[] Weights = MakeWeights();

    private static float[] MakeWeights()
    {
        float[] weights = new float[Taps * 2 + 1];
        float total = 0;
        for (int i = -Taps; i <= Taps; i++)
        {
            float f = i / 5f;
            weights[i + Taps] = MathF.Exp(-0.5f * f * f);
            total += weights[i + Taps];
        }

        for (int i = 0; i < weights.Length; i++)
            weights[i] /= total;
        return weights;
    }

    private static float Step(float radius) => MathF.Max(1f, radius / 8f);

    /// <summary>How far the halo reaches past the glyph, in pixels.</summary>
    public static int Padding(float radius) => (int)MathF.Ceiling(Taps * Step(radius)) + 2;

    /// <summary>
    /// Blurs a coverage <paramref name="mask"/> (0..1, rows top first, with
    /// <see cref="Padding"/> of clear room around what it covers) in place, and
    /// returns its alpha scaled by the intensity and clamped, as bytes.
    /// </summary>
    public static byte[] Blur(float[] mask, int w, int h, float radius, float intensity)
    {
        float step = Step(radius);
        float[] across = new float[w * h];
        Pass(mask, across, w, h, step, 1, 0);
        Pass(across, mask, w, h, step, 0, 1);

        byte[] coverage = new byte[w * h];
        for (int i = 0; i < coverage.Length; i++)
            coverage[i] = (byte)(Math.Clamp(mask[i] * intensity, 0f, 1f) * 255f + 0.5f);
        return coverage;
    }

    /// <summary>One axis, sampling between pixels linearly as the GPU's filter did; outside is clear.</summary>
    private static void Pass(float[] source, float[] target, int w, int h, float step, int dx, int dy)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                for (int i = -Taps; i <= Taps; i++)
                {
                    float at = (dx != 0 ? x : y) + i * step;
                    int low = (int)MathF.Floor(at);
                    float t = at - low;
                    sum += Weights[i + Taps] * (
                        Sample(source, w, h, dx != 0 ? low : x, dx != 0 ? y : low) * (1 - t) +
                        Sample(source, w, h, dx != 0 ? low + 1 : x, dx != 0 ? y : low + 1) * t);
                }

                target[y * w + x] = sum;
            }
    }

    private static float Sample(float[] source, int w, int h, int x, int y) =>
        x < 0 || y < 0 || x >= w || y >= h ? 0f : source[y * w + x];
}
