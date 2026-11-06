using System;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Notes;

/// <summary>
/// The game's guide line between consecutive targets, and a spark that runs it:
/// it leaves a target on that target's beat and lands on the next on its own,
/// eating the line behind it, so the spark is always where the player should be.
/// </summary>
public partial class NoteApproachField
{
    /// <summary>The game's guide: <c>PlayfieldRenderer.GuideColor</c> at 60%, 2 px.</summary>
    private static readonly Color GuideColor = Palette.Text;

    private const float GuideAlpha = 0.6f;

    private const float SparkRadius = 7f;

    /// <summary>How far back along its leg the spark's tail reaches, as a share of the leg.</summary>
    private const float SparkTail = 0.18f;

    private const int SparkTailSteps = 8;

    /// <summary>Embers shed behind the spark, each living this long.</summary>
    private const int Embers = 7;

    private const float EmberMs = 260f;

    /// <summary>The hub dot where a chord's spokes meet, as a share of the note radius, as in the game.</summary>
    private const float HubRadiusScale = 0.18f;

    /// <summary>One beat's notes: where the guide attaches and how far short of it the line stops.</summary>
    private readonly record struct Moment(int First, int Count, double TimeMs, Vector2 Anchor, float Clearance);

    private readonly System.Collections.Generic.List<Moment> _moments = [];

    private void DrawGuides(float approach)
    {
        // The chart is written in time order, so a chord is a run of equal times.
        _moments.Clear();
        for (int i = 0; i < _notes.Count;)
        {
            int count = 1;
            Vector2 sum = _notes[i].Position;
            while (i + count < _notes.Count && _notes[i + count].TimeMs == _notes[i].TimeMs)
                sum += _notes[i + count++].Position;
            _moments.Add(count == 1
                ? new Moment(i, 1, _notes[i].TimeMs, sum, Radius)
                : new Moment(i, count, _notes[i].TimeMs, sum / count, Radius * HubRadiusScale));
            i += count;
        }

        for (int m = 0; m < _moments.Count; m++)
        {
            Moment to = _moments[m];
            if (_now >= to.TimeMs)
                continue;
            float shownTo = approach - (float)(to.TimeMs - _now);
            if (shownTo < 0f)
                break;

            float alphaTo = MathF.Min(1f, shownTo / PopInMs);
            if (to.Count > 1)
                DrawHub(to, alphaTo);
            if (m == 0)
                continue;

            Moment from = _moments[m - 1];
            float alpha = MathF.Min(Appeared(_notes[from.First], approach), alphaTo);
            Vector2 leg = to.Anchor - from.Anchor;
            float length = leg.Length();
            if (length <= from.Clearance + to.Clearance)
                continue;
            Vector2 dir = leg / length;
            Vector2 start = from.Anchor + dir * from.Clearance, end = to.Anchor - dir * to.Clearance;

            if (_now < from.TimeMs)
            {
                DrawLine(start, end, Fade(GuideColor, GuideAlpha * alpha), 2f, true);
                continue;
            }

            float u = (float)((_now - from.TimeMs) / (to.TimeMs - from.TimeMs));
            Vector2 spark = start.Lerp(end, u);
            DrawLine(spark, end, Fade(GuideColor, GuideAlpha * alpha), 2f, true);
            DrawSpark(_notes[from.First], _notes[to.First], start, end, u, alpha);
        }
    }

    /// <summary>A chord's hub: a dot at its middle with a spoke out to each note's rim.</summary>
    private void DrawHub(Moment moment, float alpha)
    {
        for (int n = moment.First; n < moment.First + moment.Count; n++)
        {
            Vector2 out_ = _notes[n].Position - moment.Anchor;
            float length = out_.Length();
            if (length <= Radius + moment.Clearance)
                continue;
            Vector2 dir = out_ / length;
            DrawLine(moment.Anchor + dir * moment.Clearance, _notes[n].Position - dir * Radius,
                Fade(GuideColor, 0.8f * alpha), 2f, true);
        }

        DrawCircle(moment.Anchor, Radius * HubRadiusScale * (1f + _pulse * 0.4f), Fade(GuideColor, alpha));
    }

    /// <summary>How far a target has popped in; a target already hit or missed counts as fully shown.</summary>
    private float Appeared(ChartNote note, float approach)
    {
        if (_now >= note.TimeMs || note.Hit || note.Missed)
            return 1f;
        return Math.Clamp((approach - (float)(note.TimeMs - _now)) / PopInMs, 0f, 1f);
    }

    /// <summary>A white-hot core in the next note's colour, a fading tail along the line and embers falling off it.</summary>
    private void DrawSpark(ChartNote from, ChartNote to, Vector2 start, Vector2 end, float u, float alpha)
    {
        Color color = to.Color.Lightened(_pulse * BeatLighten);
        Vector2 across = (end - start).Normalized().Orthogonal();

        for (int s = SparkTailSteps; s >= 1; s--)
        {
            float back = Math.Max(0f, u - SparkTail * s / SparkTailSteps);
            float k = 1f - s / (float)(SparkTailSteps + 1);
            DrawCircle(start.Lerp(end, back), SparkRadius * (0.35f + 0.65f * k), Fade(color, alpha * k * 0.45f));
        }

        // Embers: each restarts on its own phase, drifting off the line where the spark just was.
        float legMs = (float)(to.TimeMs - from.TimeMs);
        for (int e = 0; e < Embers; e++)
        {
            float phase = (float)(_now / EmberMs) + e / (float)Embers;
            float age = phase - MathF.Floor(phase);
            float born = Math.Max(0f, u - age * EmberMs / legMs);
            float side = MathF.Sin((MathF.Floor(phase) + e) * 12.9898f + from.Spin) * 2f - 1f;
            Vector2 at = start.Lerp(end, born) + across * side * age * 22f + new Vector2(0, age * 14f);
            DrawCircle(at, 2.6f * (1f - age), Fade(Palette.TextBright.Lerp(color, age), alpha * (1f - age)));
        }

        Vector2 head = start.Lerp(end, u);
        float swell = 1f + _pulse * 0.5f;
        DrawCircle(head, SparkRadius * 2.2f * swell, Fade(color, alpha * 0.25f));
        DrawCircle(head, SparkRadius * swell, Fade(color, alpha));
        DrawCircle(head, SparkRadius * 0.5f * swell, Fade(Palette.TextBright, alpha));
    }
}
