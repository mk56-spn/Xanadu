using System;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Notes;

public partial class NoteApproachField
{
    /// <summary>How far a flying note starts from its spot.</summary>
    private const float FlyDistance = 560f;

    /// <summary>How far a dropping note falls: its speed is this over the approach, like a lane's scroll speed.</summary>
    private const float FallDistance = 640f;

    /// <summary>How long a target or slot takes to pop in.</summary>
    private const float PopInMs = 200f;

    private const int TrailSteps = 14;

    /// <summary>How much a note swells on the beat, as a share of its radius; slots swell a little less.</summary>
    private const float NoteBeatSwell = 0.12f;

    private const float SlotBeatSwell = 0.08f;

    /// <summary>How far toward white a note's colour goes on the beat.</summary>
    private const float BeatLighten = 0.35f;

    /// <summary>1 on each beat, dying away quickly before the next.</summary>
    private float _pulse;

    private static readonly Color Edge = Palette.TextBright;
    private static readonly Color Core = Palette.SurfaceAlt;

    public override void _Draw()
    {
        float approach = _experiment.ApproachMs;
        float pulse = _pulse = MathF.Exp(-BeatPhase() * 7f);

        if (_experiment.Style == NoteApproachStyle.DivaLine)
            DrawGuides(approach);

        // Back to front, so earlier notes, which are about to be hit, sit on top.
        for (int i = _notes.Count - 1; i >= 0; i--)
        {
            ChartNote note = _notes[i];
            float remaining = (float)(note.TimeMs - _now);
            if (remaining > approach || note.Hit || note.Missed)
                continue;

            float left = Math.Max(remaining, 0f) / approach;
            float shown = approach - remaining;
            switch (_experiment.Style)
            {
                case NoteApproachStyle.Ring: DrawRingApproach(note, left, pulse); break;
                case NoteApproachStyle.Diva or NoteApproachStyle.DivaLine: DrawDivaApproach(note, left, shown, pulse); break;
                default: DrawDropApproach(note, left, shown, pulse); break;
            }
        }

        foreach (ChartNote note in _notes)
        {
            if (note.Hit)
                DrawBurst(note, (float)(_now - note.HitMs));
            else if (note.Missed)
                DrawMiss(note, (float)(_now - note.MissMs));
            if (note.Judgement.Length > 0)
                DrawJudgement(note, (float)(_now - (note.Hit ? note.HitMs : note.MissMs)));
        }
    }

    /// <summary>The game's look: alpha rises as the ring, 9 radii out at first, shrinks onto the note.</summary>
    private void DrawRingApproach(ChartNote note, float left, float pulse)
    {
        float alpha = 1f - left;
        Color ring = note.Color.Lightened(pulse * BeatLighten);
        DrawArc(note.Position, Radius * (1f + left * 8f), 0, Mathf.Tau, 96,
            Fade(ring, alpha * (0.75f + 0.25f * pulse)), 2f + 2f * pulse, true);
        DrawNote(note.Position, Radius, note.Color, alpha);
    }

    /// <summary>
    /// The target pops in with a clock hand that comes round to twelve on the
    /// beat; the note flies in from <see cref="ChartNote.FlyFrom"/> on a wave
    /// that dies out as it lands, trailing its colour.
    /// </summary>
    private void DrawDivaApproach(ChartNote note, float left, float shown, float pulse)
    {
        float pop = PopIn(shown);
        DrawSlot(note, pop * (1f + pulse * SlotBeatSwell), MathF.Min(1f, shown / PopInMs));

        float hand = -Mathf.Pi / 2 + Mathf.Tau * (1f - left);
        DrawLine(note.Position, note.Position + Vector2.FromAngle(hand) * Radius * 0.78f * pop,
            Fade(Edge, 0.9f), 4f, true);
        DrawCircle(note.Position, 4f * pop, Edge);

        float alpha = MathF.Min(1f, shown / 120f);
        DrawTrail(note, left, alpha);
        DrawNote(FlyPoint(note, left), Radius, note.Color, alpha);
    }

    /// <summary>A ribbon behind a flying note, the note's width at its head, thinning and fading to nothing.</summary>
    private void DrawTrail(ChartNote note, float left, float alpha)
    {
        Span<Vector2> quad = stackalloc Vector2[4];
        Color[] colors = TrailColors;
        Vector2 head = FlyPoint(note, left);
        Vector2 headSide = Vector2.Zero;
        for (int s = 1; s <= TrailSteps; s++)
        {
            Vector2 tail = FlyPoint(note, Math.Min(1f, left + s * 0.012f));
            if (tail.DistanceSquaredTo(head) < 0.25f)
                break;
            Vector2 across = (head - tail).Normalized().Orthogonal();
            float k0 = 1f - (s - 1) / (float)TrailSteps, k1 = 1f - s / (float)TrailSteps;
            Vector2 tailSide = across * Radius * 0.85f * k1;
            if (s == 1)
                headSide = across * Radius * 0.85f * k0;
            quad[0] = head + headSide;
            quad[1] = tail + tailSide;
            quad[2] = tail - tailSide;
            quad[3] = head - headSide;
            colors[0] = colors[3] = Fade(note.Color, alpha * k0 * 0.5f);
            colors[1] = colors[2] = Fade(note.Color, alpha * k1 * 0.5f);
            DrawPolygon(quad.ToArray(), colors);
            head = tail;
            headSide = tailSide;
        }
    }

    private static readonly Color[] TrailColors = new Color[4];

    private static Vector2 FlyPoint(ChartNote note, float left)
    {
        Vector2 across = new(-note.FlyFrom.Y, note.FlyFrom.X);
        return note.Position + note.FlyFrom * FlyDistance * left
               + across * note.Wave * MathF.Sin(left * Mathf.Pi * 1.5f);
    }

    /// <summary>
    /// A slot opens on the spot, with a faint column above it; the note falls
    /// straight down at one speed, coming in under the stage's top edge.
    /// </summary>
    private void DrawDropApproach(ChartNote note, float left, float shown, float pulse)
    {
        float open = MathF.Min(1f, shown / PopInMs);
        float top = MathF.Max(_area.Position.Y, note.Position.Y - FallDistance);
        var column = new Rect2(note.Position.X - Radius, top, Radius * 2, note.Position.Y - top);
        DrawRect(column, Fade(note.Color, 0.05f * open));
        DrawLine(new Vector2(column.Position.X, top), new Vector2(column.Position.X, note.Position.Y),
            Fade(note.Color, 0.18f * open), 1.5f, true);
        DrawLine(new Vector2(column.End.X, top), new Vector2(column.End.X, note.Position.Y),
            Fade(note.Color, 0.18f * open), 1.5f, true);

        float scale = PopIn(shown) * (1f + pulse * SlotBeatSwell);
        DrawSlot(note, scale, open);

        // The slot's cup: open at the top, which is where the note comes in.
        float cup = (Radius + 9f) * scale;
        Color cupColor = Fade(Edge, 0.85f * open);
        DrawArc(note.Position, cup, 0, Mathf.Pi, 48, cupColor, 4f, true);
        DrawLine(note.Position + new Vector2(-cup, 0), note.Position + new Vector2(-cup, -cup * 0.9f), cupColor, 4f, true);
        DrawLine(note.Position + new Vector2(cup, 0), note.Position + new Vector2(cup, -cup * 0.9f), cupColor, 4f, true);

        var at = new Vector2(note.Position.X, note.Position.Y - FallDistance * left);
        float edge = Math.Clamp((at.Y - _area.Position.Y) / (Radius * 1.5f), 0f, 1f);
        float alpha = edge * MathF.Min(1f, shown / 100f);
        float streak = Radius * 2.2f;
        DrawLine(at, at - new Vector2(0, streak), Fade(note.Color, alpha * 0.35f), Radius * 0.9f, true);
        DrawNote(at, Radius, note.Color, alpha);
    }

    /// <summary>A pop from 60% with a small overshoot.</summary>
    private static float PopIn(float shown)
    {
        float t = MathF.Min(1f, shown / PopInMs) - 1f;
        const float back = 2.2f;
        return 0.6f + 0.4f * (1f + (back + 1f) * t * t * t + back * t * t);
    }

    /// <summary>Where a note lands: a dark well with a ring in its colour and the core outlined faintly.</summary>
    private void DrawSlot(ChartNote note, float scale, float alpha)
    {
        float r = Radius * scale;
        DrawCircle(note.Position, r, Fade(Palette.SurfaceDeep, 0.7f * alpha));
        DrawArc(note.Position, r, 0, Mathf.Tau, 64, Fade(note.Color.Lightened(_pulse * BeatLighten), 0.9f * alpha), 3f + 2f * _pulse, true);
        DrawArc(note.Position, r * 20f / 36f, 0, Mathf.Tau, 48, Fade(Edge, 0.35f * alpha), 2f, true);
    }

    /// <summary>The hit note from the designs: lane colour ring, white line work, dark core.</summary>
    private void DrawNote(Vector2 center, float radius, Color color, float alpha, bool onBeat = true)
    {
        if (alpha <= 0f)
            return;
        if (onBeat)
        {
            radius *= 1f + _pulse * NoteBeatSwell;
            color = color.Lightened(_pulse * BeatLighten);
        }
        float k = radius / 36f;
        DrawCircle(center, radius, Fade(color, alpha));
        DrawArc(center, radius, 0, Mathf.Tau, 64, Fade(Edge, alpha), 4f * k, true);
        DrawCircle(center, 20f * k, Fade(Core, alpha));
        DrawArc(center, 20f * k, 0, Mathf.Tau, 48, Fade(Edge, alpha), 5f * k, true);
    }

    /// <summary>A white flash, a ring thrown outward and sparks along the note's spin.</summary>
    private void DrawBurst(ChartNote note, float age)
    {
        if (age > BurstMs)
            return;
        float t = age / BurstMs;
        float ease = 1f - (1f - t) * (1f - t);
        if (t < 0.35f)
            DrawCircle(note.Position, Radius * (1f + 0.25f * t), Fade(Edge, 0.85f * (1f - t / 0.35f)));
        DrawArc(note.Position, Radius * (1f + 1.2f * ease), 0, Mathf.Tau, 64,
            Fade(note.Color, 1f - t), 7f * (1f - t) + 1f, true);
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = Vector2.FromAngle(note.Spin + i * Mathf.Tau / 8);
            float inner = Radius * (1.1f + 1.1f * ease), outer = Radius * (1.35f + 1.7f * ease);
            DrawLine(note.Position + dir * inner, note.Position + dir * outer, Fade(Edge, 1f - t), 3f, true);
        }
    }

    /// <summary>A missed note greys out and sinks.</summary>
    private void DrawMiss(ChartNote note, float age)
    {
        if (age > BurstMs)
            return;
        float t = age / BurstMs;
        DrawNote(note.Position + new Vector2(0, 24f * t), Radius, Palette.TextMuted, 0.6f * (1f - t), onBeat: false);
    }

    private void DrawJudgement(ChartNote note, float age)
    {
        if (age > JudgementMs)
            return;
        float t = age / JudgementMs;
        var at = note.Position + new Vector2(-120f, -Radius - 14f - 24f * t);
        Color color = note.Missed ? Palette.TextMuted : note.Judgement.StartsWith('P') ? Palette.Accent : Edge;
        DrawString(_font, at, note.Judgement, HorizontalAlignment.Center, 240f, 22, Fade(color, 1f - t * t));
    }

    private static Color Fade(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
}
