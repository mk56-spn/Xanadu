using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The strip along the bottom of the preview while clips play into each
/// other: one block per step, sized by how long it plays, each hand-over's
/// fade drawn as its weight curve, and a playhead. Over it, the blend on
/// screen right now, as each clip's share of the pose.
/// </summary>
public sealed class AnimatorFlowOverlay(IRectCanvas canvas, Rid target)
{
    public const float Height = 52f;
    private const float Inset = 16f;
    private const float LabelHeight = 16f;
    private const int CurveSegments = 24;

    private static readonly Color Backdrop = new(Palette.SurfaceDeep, 0.88f);
    private static readonly Color StepA = new(Palette.Info, 0.28f);
    private static readonly Color StepB = new(Palette.Cyan, 0.22f);
    private static readonly Color FadeFill = new(Palette.Accent, 0.22f);
    private static readonly Color Curve = Palette.Accent;
    private static readonly Color Blocked = new(Palette.Warning, 0.45f);
    private static readonly Color Playhead = Colors.White;

    /// <summary>The strip's rectangle inside the preview area.</summary>
    public static Rect2 Bounds(Rect2 area) => new(
        area.Position.X + Inset,
        area.End.Y - Height - Inset,
        Math.Max(0f, area.Size.X - Inset * 2),
        Height);

    public void Draw(Entity session, Rect2 area)
    {
        AnimatorSequence sequence = session.GetComponent<AnimatorSequence>();
        if (sequence.Steps is not { Count: > 0 } steps)
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        Rect2 bar = Bounds(area);
        float total = 0f;
        foreach (AnimatorSequenceStep step in steps)
            total += step.Seconds;
        if (total <= 0f || bar.Size.X <= 0f)
            return;

        canvas.DrawRect(target, bar.Grow(4f), Backdrop);
        float scale = bar.Size.X / total;
        Rect2 track = new(bar.Position + new Vector2(0, LabelHeight), new Vector2(bar.Size.X, bar.Size.Y - LabelHeight));

        float x = bar.Position.X;
        for (int i = 0; i < steps.Count; i++)
        {
            float width = steps[i].Seconds * scale;
            var block = new Rect2(x, track.Position.Y, width, track.Size.Y);
            canvas.DrawRect(target, block, i % 2 == 0 ? StepA : StepB);
            canvas.DrawTextStyledLeft(target,
                new Rect2(x + 4, bar.Position.Y, Math.Max(0f, width - 8), LabelHeight),
                steps[i].Clip.ToUpperInvariant(), Palette.TextMuted, 11);

            if (i > 0)
                DrawFade(document.Package, steps[i - 1].Clip, steps[i].Clip, x, scale, track);
            x += width;
        }

        float elapsed = sequence.Elapsed;
        for (int i = 0; i < Math.Min(sequence.Index, steps.Count); i++)
            elapsed += steps[i].Seconds;
        float head = bar.Position.X + Math.Clamp(elapsed, 0f, total) * scale;
        canvas.DrawLine(target, new Vector2(head, track.Position.Y - 2), new Vector2(head, track.End.Y + 2), Playhead, 2f);

        canvas.DrawTextStyledRight(target,
            new Rect2(bar.Position.X, bar.Position.Y, bar.Size.X - 4, LabelHeight),
            Blend(document.Runtime), Palette.Text, 11);
    }

    /// <summary>The hand-over into a step: its fade's span shaded and its weight curve rising across it.</summary>
    private void DrawFade(XafPackage package, string from, string to, float x, float scale, Rect2 track)
    {
        if (from == to)
            return;

        if (!XafTransitions.Allows(package, from, to))
        {
            // The game refuses this one; mark the boundary rather than a fade.
            canvas.DrawRect(target, new Rect2(x - 2, track.Position.Y, 4, track.Size.Y), Blocked);
            return;
        }

        (float fade, XafFadeCurve curve) = XafTransitions.Blend(package, from, to);
        float width = fade * scale;
        if (width < 1f)
        {
            canvas.DrawLine(target, new Vector2(x, track.Position.Y), new Vector2(x, track.End.Y), Curve, 2f);
            return;
        }

        canvas.DrawRect(target, new Rect2(x, track.Position.Y, width, track.Size.Y), FadeFill);
        var points = new List<Vector2>(CurveSegments + 1);
        for (int i = 0; i <= CurveSegments; i++)
        {
            float t = (float)i / CurveSegments;
            float weight = XafTransitions.Weight(curve, t);
            points.Add(new Vector2(x + t * width, track.End.Y - weight * track.Size.Y));
        }
        canvas.DrawPolyline(target, points.ToArray(), Curve, 2f);
    }

    /// <summary>Each clip's share of the pose on screen, or the clip alone when nothing is fading.</summary>
    public static string Blend(AnimatorRuntime runtime)
    {
        if (!runtime.IsCrossfading)
            return $"{runtime.CurrentClip.ToUpperInvariant()} 100%";

        int next = (int)MathF.Round(runtime.FadeWeight * 100f);
        return $"{runtime.CurrentClip.ToUpperInvariant()} {100 - next}%  ▸  {runtime.NextClip.ToUpperInvariant()} {next}%";
    }
}
