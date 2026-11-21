using System;
using Xanadu.Core.Maths;

namespace Xanadu.Screens.Play;

/// <summary>
/// How the judgement text lands, by its age in ms: it slams in big and
/// settles with a little overshoot, starts white and turns its own colour,
/// drifts up and fades out at the end of its <see cref="ShowMs"/>.
/// </summary>
public static class JudgementPop
{
    /// <summary>How long a judgement shows, ms.</summary>
    public const float ShowMs = 700f;

    private const int BaseSize = 28;

    /// <summary>How big the text slams in, as a share of its size.</summary>
    private const float SlamScale = 1.7f;

    /// <summary>How long the slam takes to settle, ms.</summary>
    private const float SettleMs = 140f;

    /// <summary>How long the white flash takes to turn to the judgement's colour, ms.</summary>
    private const float FlashMs = 90f;

    /// <summary>How far it drifts up over its whole show, pixels.</summary>
    private const float RisePixels = 26f;

    /// <summary>How long the fade at the end of its show runs, ms.</summary>
    private const float FadeMs = 220f;

    public static int FontSize(float age)
    {
        float t = Math.Clamp(age / SettleMs, 0f, 1f);
        // Back-out ease: from the slam size, past the rest size a touch, then home.
        const float back = 2.2f;
        float u = t - 1f;
        float settle = 1f + u * u * ((back + 1f) * u + back);
        float scale = SlamScale + (1f - SlamScale) * settle;
        return Math.Max(1, (int)MathF.Round(BaseSize * scale));
    }

    public static Color Color(Color color, float age)
    {
        float flash = 1f - Math.Clamp(age / FlashMs, 0f, 1f);
        Color shown = color.Lerp(Colors.White, flash * flash);
        return shown with { A = color.A * Fade(age) };
    }

    public static float Rise(float age) =>
        RisePixels * (1f - MathF.Pow(1f - Math.Clamp(age / ShowMs, 0f, 1f), 3f));

    public static float Fade(float age) =>
        Math.Clamp((ShowMs - age) / FadeMs, 0f, 1f);
}
