using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The DIVA+ note look, brought over from the notes lab (<c>Tools/Lab/Experiments/Notes</c>):
/// a target pops in on the spot with a clock hand coming round to twelve on
/// the beat, the note flies in onto it on a dying wave, trailing its colour,
/// and a spark runs the guide line from the moment just hit to the next.
/// Play only, while the playfield has <see cref="DivaNotes"/>; F8 swaps back.
/// </summary>
public static partial class PlayfieldRenderer
{
    /// <summary>How long a note shows before its beat: the lab's default.</summary>
    public const float DivaApproachMs = 1200f;

    /// <summary>How far a flying note starts from its spot, in playfield units.</summary>
    private const float DivaFlyDistance = 560f;

    /// <summary>How long a target takes to pop in.</summary>
    private const float DivaPopInMs = 200f;

    /// <summary>How long a flying note takes to fade in.</summary>
    private const float DivaFadeInMs = 120f;

    private const int DivaTrailSteps = 14;

    /// <summary>How much a note swells on the beat, as a share of its radius; targets swell a little less.</summary>
    private const float DivaNoteSwell = 0.12f;

    private const float DivaSlotSwell = 0.08f;

    /// <summary>How far toward white a note's colour goes on the beat.</summary>
    private const float DivaBeatLighten = 0.35f;

    private const float DivaGuideAlpha = 0.6f;

    private const float DivaSparkRadius = 7f;

    /// <summary>How far back along its leg the spark's tail reaches, as a share of the leg.</summary>
    private const float DivaSparkTail = 0.18f;

    private const int DivaSparkTailSteps = 8;

    private const int DivaEmbers = 7;

    private const float DivaEmberMs = 260f;

    /// <summary>One beat's notes: when, where the guide attaches, how many and the first one's colour.</summary>
    public readonly record struct DivaMoment(long TimeMs, Vector2 Anchor, int Count, Color Color, float Appeared, int First);

    private readonly record struct DivaPoint(long TimeMs, Vector2 At, Color Color, float Appeared);

    private static readonly List<DivaPoint> DivaPoints = new();
    private static readonly List<DivaMoment> DivaMoments = new();
    private static readonly Vector2[] DivaTrailPoints = new Vector2[DivaTrailSteps * 6];
    private static readonly Color[] DivaTrailColors = new Color[DivaTrailSteps * 6];

    /// <summary>1 on each beat of the song's tempo, dying away quickly before the next.</summary>
    public static float DivaPulse(Playhead playhead)
    {
        double beats = playhead.Tempo.BeatAt(playhead.TimeMs);
        return MathF.Exp(-(float)(beats - Math.Floor(beats)) * 7f);
    }

    /// <summary>
    /// The earliest moment still to hit, in playfield units where its notes are
    /// shown, or null when none are left. Missed notes no longer count.
    /// </summary>
    public static DivaMoment? EarliestDivaMoment(EntityStore entities)
    {
        long earliest = long.MaxValue;
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (IsDivaNote(entity, note) && note.TimeMs < earliest)
                earliest = note.TimeMs;
        }

        if (earliest == long.MaxValue)
            return null;

        Vector2 sum = Vector2.Zero;
        int count = 0;
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (IsDivaNote(entity, note) && note.TimeMs - earliest <= NoteGuides.SimultaneousMs)
            {
                sum += GroupOffset.Shown(entity, note.Position);
                count++;
            }
        }

        return new DivaMoment(earliest, sum / count, count, default, 1f, 0);
    }

    private static bool IsDivaNote(Entity entity, Note note) =>
        !NoteRules.IsRail(note.Kind) && !NoteRules.IsChannelEnd(note.Kind) && !entity.Tags.Has<NoteMissed>();

    /// <summary>
    /// One note the DIVA+ way. Once its beat has passed it sits on its spot and
    /// fades as the game's notes do, so late hits and misses read the same.
    /// </summary>
    public static void DrawDivaNote(
        IRectCanvas canvas,
        Rid rid,
        Entity entity,
        PlayfieldView view,
        Playhead playhead,
        float pulse)
    {
        Note note = entity.GetComponent<Note>();
        if (NoteRules.IsRail(note.Kind) || NoteRules.IsChannelEnd(note.Kind))
            return;

        long remaining = note.TimeMs - playhead.TimeMs;
        if (remaining > DivaApproachMs)
            return;
        if (remaining < 0 || entity.Tags.Has<NoteMissed>())
        {
            DrawLandedDivaNote(canvas, rid, entity, note, view, playhead);
            return;
        }

        float radius = NoteSize * view.Scale / 2;
        float k = radius / DesignRadius;
        Vector2 center = view.PlayfieldToScreen(note.Position);
        Color lane = GroupColor.For(entity, ColorFor(note));
        Color lit = lane.Lightened(pulse * DivaBeatLighten);
        float shown = DivaApproachMs - remaining;
        float left = remaining / DivaApproachMs;
        float pop = DivaPopIn(shown);
        float open = Math.Min(1f, shown / DivaPopInMs);

        SelectItemShader(canvas, entity);

        // The target: a dark well, a ring in the note's colour and the core outlined faintly.
        float slot = radius * pop * (1f + pulse * DivaSlotSwell);
        canvas.DrawCircle(rid, center, slot, Tinted(Palette.SurfaceDeep, 0.7f * open * lane.A));
        canvas.DrawCircleOutline(rid, center, slot, Tinted(lit, 0.9f * open), (3f + 2f * pulse) * k, 64);
        canvas.DrawCircleOutline(rid, center, slot * 20f / 36f, Tinted(NoteEdgeColor, 0.35f * open * lane.A), 2f * k, 48);

        // The clock hand reaches twelve on the beat.
        float hand = -Mathf.Pi / 2 + Mathf.Tau * (1f - left);
        canvas.DrawLine(rid, center, center + Vector2.FromAngle(hand) * radius * 0.78f * pop,
            Tinted(NoteEdgeColor, 0.9f * open * lane.A), 4f * k);
        canvas.DrawCircle(rid, center, 4f * k * pop, Tinted(NoteEdgeColor, open * lane.A));

        if (note.Kind == NoteKind.Dash)
            DrawDashLink(canvas, rid, note, view, radius, Tinted(lane, open));

        (Vector2 from, float wave) = DivaFlight(note.TimeMs);
        float distance = DivaFlyDistance * view.Scale;
        wave *= view.Scale;
        float alpha = Math.Min(1f, shown / DivaFadeInMs);
        DrawDivaTrail(canvas, rid, center, from, distance, wave, left, radius, Tinted(lane, alpha));
        DrawNoteBody(canvas, rid, note, DivaFlyPoint(center, from, distance, wave, left),
            radius * (1f + pulse * DivaNoteSwell), Tinted(lit, alpha));

        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    /// <summary>A note past its beat or missed: on its spot, fading out like the game's own.</summary>
    private static void DrawLandedDivaNote(IRectCanvas canvas, Rid rid, Entity entity, Note note, PlayfieldView view, Playhead playhead)
    {
        float alpha = NoteFade.Alpha(note, playhead.TimeMs);
        if (alpha <= 0)
            return;

        Vector2 center = view.PlayfieldToScreen(note.Position);
        float radius = NoteSize * view.Scale / 2;
        Color lane = GroupColor.For(entity, ColorFor(note));
        Color color = entity.Tags.Has<NoteMissed>()
            ? new Color(0.4f, 0.4f, 0.5f, alpha * lane.A * 0.45f)
            : Tinted(lane, alpha);

        SelectItemShader(canvas, entity);
        if (note.Kind == NoteKind.Dash)
            DrawDashLink(canvas, rid, note, view, radius, color);
        DrawNoteBody(canvas, rid, note, center, radius, color);
        canvas.SelectCommonShader("");
        canvas.SelectShader(0);
    }

    /// <summary>A pop from 60% with a small overshoot.</summary>
    private static float DivaPopIn(float shown)
    {
        float t = MathF.Min(1f, shown / DivaPopInMs) - 1f;
        const float back = 2.2f;
        return 0.6f + 0.4f * (1f + (back + 1f) * t * t * t + back * t * t);
    }

    /// <summary>
    /// Which way a note flies in from and its wave, from its time alone, so a
    /// chord's notes come in together as one formation and replays look the same.
    /// </summary>
    private static (Vector2 From, float Wave) DivaFlight(long timeMs)
    {
        uint h = unchecked((uint)timeMs * 2654435761u);
        h ^= h >> 15;
        h = unchecked(h * 2246822519u);
        h ^= h >> 13;
        float angle = (h & 0xFFFF) / 65536f * Mathf.Tau;
        float side = (h & 0x10000) != 0 ? 1f : -1f;
        float size = 40f + 30f * ((h >> 17) & 0xFF) / 255f;
        return (Vector2.FromAngle(angle), side * size);
    }

    private static Vector2 DivaFlyPoint(Vector2 center, Vector2 from, float distance, float wave, float left)
    {
        Vector2 across = new(-from.Y, from.X);
        return center + from * distance * left + across * wave * MathF.Sin(left * Mathf.Pi * 1.5f);
    }

    /// <summary>A ribbon behind a flying note, the note's width at its head, thinning and fading to nothing.</summary>
    private static void DrawDivaTrail(
        IRectCanvas canvas,
        Rid rid,
        Vector2 center,
        Vector2 from,
        float distance,
        float wave,
        float left,
        float radius,
        Color color)
    {
        Vector2[] points = DivaTrailPoints;
        Color[] colors = DivaTrailColors;
        int used = 0;
        Vector2 head = DivaFlyPoint(center, from, distance, wave, left);
        Vector2 headSide = Vector2.Zero;
        for (int s = 1; s <= DivaTrailSteps; s++)
        {
            Vector2 tail = DivaFlyPoint(center, from, distance, wave, Math.Min(1f, left + s * 0.012f));
            if (tail.DistanceSquaredTo(head) < 0.25f)
                break;
            Vector2 across = (head - tail).Normalized().Orthogonal();
            float k0 = 1f - (s - 1) / (float)DivaTrailSteps, k1 = 1f - s / (float)DivaTrailSteps;
            Vector2 tailSide = across * radius * 0.85f * k1;
            if (s == 1)
                headSide = across * radius * 0.85f * k0;

            Color near = Tinted(color, k0 * 0.5f), far = Tinted(color, k1 * 0.5f);
            points[used] = head + headSide; colors[used++] = near;
            points[used] = tail + tailSide; colors[used++] = far;
            points[used] = tail - tailSide; colors[used++] = far;
            points[used] = head + headSide; colors[used++] = near;
            points[used] = tail - tailSide; colors[used++] = far;
            points[used] = head - headSide; colors[used++] = near;
            head = tail;
            headSide = tailSide;
        }

        if (used > 0)
            canvas.DrawTriangles(rid, points.AsSpan(0, used), colors.AsSpan(0, used));
    }

    /// <summary>
    /// The guide between the moments on screen, hubs for chords, and the spark
    /// on the leg from the moment just hit (<see cref="DivaSpark"/>) to the next.
    /// </summary>
    public static void DrawDivaGuides(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Playhead playhead,
        float pulse)
    {
        float radius = NoteSize * view.Scale / 2;
        float hubRadius = radius * NoteGuides.HubRadiusScale;
        GatherDivaMoments(entities, view, playhead.TimeMs);

        for (int m = 0; m < DivaMoments.Count; m++)
        {
            DivaMoment to = DivaMoments[m];
            if (to.Count > 1)
                DrawDivaHub(canvas, rid, to, radius, hubRadius, pulse);
            if (m == 0)
                continue;

            DivaMoment from = DivaMoments[m - 1];
            if (Trim(from.Anchor, from.Count > 1 ? hubRadius : radius, to.Anchor, to.Count > 1 ? hubRadius : radius)
                is { } leg)
            {
                canvas.DrawLine(rid, leg.Start, leg.End,
                    Faded(GuideColor, DivaGuideAlpha * Math.Min(from.Appeared, to.Appeared)), 2);
            }
        }

        if (DivaMoments.Count == 0 || !TryGetSpark(entities, out DivaSpark spark) || !spark.HasFrom ||
            spark.NextMs != DivaMoments[0].TimeMs)
        {
            return;
        }

        DivaMoment next = DivaMoments[0];
        if (Trim(view.PlayfieldToScreen(spark.From), spark.FromChord ? hubRadius : radius,
                next.Anchor, next.Count > 1 ? hubRadius : radius) is not { } sparkLeg)
        {
            return;
        }

        float alpha = next.Appeared;
        if (playhead.TimeMs < spark.FromMs)
        {
            // Hit early: the spark waits for the beat it leaves on.
            canvas.DrawLine(rid, sparkLeg.Start, sparkLeg.End, Faded(GuideColor, DivaGuideAlpha * alpha), 2);
            return;
        }

        float u = Math.Clamp((playhead.TimeMs - spark.FromMs) / (float)(next.TimeMs - spark.FromMs), 0f, 1f);
        Vector2 head = sparkLeg.Start.Lerp(sparkLeg.End, u);
        canvas.DrawLine(rid, head, sparkLeg.End, Faded(GuideColor, DivaGuideAlpha * alpha), 2);
        DrawDivaSpark(canvas, rid, sparkLeg.Start, sparkLeg.End, u, next.Color.Lightened(pulse * DivaBeatLighten),
            alpha, view.Scale, playhead.TimeMs, next.TimeMs - spark.FromMs, spark.FromMs, pulse);
    }

    private static bool TryGetSpark(EntityStore entities, out DivaSpark spark)
    {
        foreach (Entity entity in entities.Query<DivaSpark>().ToEntityList())
        {
            spark = entity.GetComponent<DivaSpark>();
            return true;
        }

        spark = default;
        return false;
    }

    private static (Vector2 Start, Vector2 End)? Trim(Vector2 from, float fromClearance, Vector2 to, float toClearance)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length <= fromClearance + toClearance)
            return null;
        Vector2 dir = delta / length;
        return (from + dir * fromClearance, to - dir * toClearance);
    }

    /// <summary>The moments on screen, earliest first, with each one's notes in <see cref="DivaPoints"/>.</summary>
    private static void GatherDivaMoments(EntityStore entities, PlayfieldView view, long playheadMs)
    {
        DivaPoints.Clear();
        DivaMoments.Clear();
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            long remaining = note.TimeMs - playheadMs;
            if (!IsDivaNote(entity, note) || remaining > DivaApproachMs)
                continue;
            if (remaining < 0 && NoteFade.Alpha(note, playheadMs) <= 0)
                continue;

            float appeared = remaining <= 0 ? 1f : Math.Clamp((DivaApproachMs - remaining) / DivaPopInMs, 0f, 1f);
            DivaPoints.Add(new DivaPoint(
                note.TimeMs,
                view.PlayfieldToScreen(GroupOffset.Shown(entity, note.Position)),
                GroupColor.For(entity, ColorFor(note)),
                appeared));
        }

        DivaPoints.Sort(static (a, b) => a.TimeMs.CompareTo(b.TimeMs));
        for (int i = 0; i < DivaPoints.Count;)
        {
            int count = 1;
            Vector2 sum = DivaPoints[i].At;
            float appeared = DivaPoints[i].Appeared;
            while (i + count < DivaPoints.Count &&
                   DivaPoints[i + count].TimeMs - DivaPoints[i].TimeMs <= NoteGuides.SimultaneousMs)
            {
                sum += DivaPoints[i + count].At;
                appeared = Math.Max(appeared, DivaPoints[i + count].Appeared);
                count++;
            }

            DivaMoments.Add(new DivaMoment(DivaPoints[i].TimeMs, sum / count, count, DivaPoints[i].Color, appeared, i));
            i += count;
        }
    }

    /// <summary>A chord's hub: a dot at its middle with a spoke out to each note's rim.</summary>
    private static void DrawDivaHub(IRectCanvas canvas, Rid rid, DivaMoment moment, float radius, float hubRadius, float pulse)
    {
        for (int n = moment.First; n < moment.First + moment.Count; n++)
        {
            DivaPoint point = DivaPoints[n];
            if (Trim(moment.Anchor, hubRadius, point.At, radius) is { } spoke)
                canvas.DrawLine(rid, spoke.Start, spoke.End, Faded(GuideColor, 0.8f * point.Appeared), 2);
        }

        canvas.DrawCircle(rid, moment.Anchor, hubRadius * (1f + pulse * 0.4f), Faded(GuideColor, moment.Appeared));
    }

    /// <summary>A white-hot core in the next note's colour, a fading tail along the line and embers falling off it.</summary>
    private static void DrawDivaSpark(
        IRectCanvas canvas,
        Rid rid,
        Vector2 start,
        Vector2 end,
        float u,
        Color color,
        float alpha,
        float scale,
        long nowMs,
        long legMs,
        long seed,
        float pulse)
    {
        float sparkRadius = DivaSparkRadius * scale;
        Vector2 across = (end - start).Normalized().Orthogonal();

        for (int s = DivaSparkTailSteps; s >= 1; s--)
        {
            float back = Math.Max(0f, u - DivaSparkTail * s / DivaSparkTailSteps);
            float k = 1f - s / (float)(DivaSparkTailSteps + 1);
            canvas.DrawCircle(rid, start.Lerp(end, back), sparkRadius * (0.35f + 0.65f * k), Tinted(color, alpha * k * 0.45f));
        }

        // Embers: each restarts on its own phase, drifting off the line where the spark just was.
        float spin = DivaFlight(seed).Wave;
        for (int e = 0; e < DivaEmbers; e++)
        {
            float phase = nowMs / DivaEmberMs + e / (float)DivaEmbers;
            float age = phase - MathF.Floor(phase);
            float born = Math.Max(0f, u - age * DivaEmberMs / legMs);
            float side = MathF.Sin((MathF.Floor(phase) + e) * 12.9898f + spin) * 2f - 1f;
            Vector2 at = start.Lerp(end, born) + (across * side * age * 22f + new Vector2(0, age * 14f)) * scale;
            canvas.DrawCircle(rid, at, 2.6f * scale * (1f - age),
                Tinted(NoteEdgeColor.Lerp(color, age), alpha * (1f - age)));
        }

        Vector2 head = start.Lerp(end, u);
        float swell = 1f + pulse * 0.5f;
        canvas.DrawCircle(rid, head, sparkRadius * 2.2f * swell, Tinted(color, alpha * 0.25f));
        canvas.DrawCircle(rid, head, sparkRadius * swell, Tinted(color, alpha));
        canvas.DrawCircle(rid, head, sparkRadius * 0.5f * swell, Tinted(NoteEdgeColor, alpha));
    }
}
