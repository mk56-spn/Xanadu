using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The hit, momentum and dash note shapes from the note designs. They are
/// authored on a 36-unit radius and scaled to the note: the lane colour fills
/// the ring, white is the line work and the core is dark.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color NoteEdgeColor = Palette.TextBright;
    public static readonly Color NoteCoreColor = Palette.SurfaceAlt;

    /// <summary>The radius the note designs are drawn at.</summary>
    private const float DesignRadius = 36f;

    // The momentum arrow is drawn pointing up-left; it is turned to the note's direction.
    private static readonly Vector2[] MomentumArrow =
    [
        new(-48f, -48f),
        new(-9.816f, -48f),
        new(-25.373f, -38.101f),
        new(-23.437f, -35.042f),
        new(-29.0f, -29.0f),
        new(-35.042f, -23.437f),
        new(-38.101f, -25.373f),
        new(-48f, -9.816f),
    ];

    // Each chevron behind the arrow is one more step of strength.
    private static readonly Vector2[][] MomentumChevrons =
    [
        [new(-54.5f, -18.5f), new(-54.5f, -54.5f), new(-18.5f, -54.5f)],
        [new(-63.5f, -27.5f), new(-63.5f, -63.5f), new(-27.5f, -63.5f)],
    ];

    private static readonly float UpLeftAngle = new Vector2(-1, -1).Angle();

    /// <summary>A keynote: a filled ring around a dark core.</summary>
    private static void DrawHitShape(IRectCanvas canvas, Rid rid, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        DrawRing(canvas, rid, center, radius, color, k);
        canvas.DrawCircle(rid, center, 20f * k, Tinted(NoteCoreColor, color.A));
        canvas.DrawCircleOutline(rid, center, 20f * k, Tinted(NoteEdgeColor, color.A), 5f * k);
    }

    /// <summary>A momentum note: the ring around a dark hexagon, with an arrow and one chevron per extra strength step.</summary>
    private static void DrawMomentumShape(IRectCanvas canvas, Rid rid, Note note, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        Color edge = Tinted(NoteEdgeColor, color.A);
        DrawRing(canvas, rid, center, radius, color, k);

        Span<Vector2> hexagon = stackalloc Vector2[7];
        for (int i = 0; i < 6; i++)
        {
            float angle = -Mathf.Pi / 2 + i * Mathf.Tau / 6;
            hexagon[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22.56f * k;
        }
        hexagon[6] = hexagon[0];
        canvas.DrawPolygon(rid, hexagon[..6], Tinted(NoteCoreColor, color.A));
        canvas.DrawPolyline(rid, hexagon, edge, 4f * k);

        float turn = PlayerMovementSystem.DashDirectionVector(note.DashDirection).Angle() - UpLeftAngle;
        Span<Vector2> arrow = stackalloc Vector2[MomentumArrow.Length + 1];
        for (int i = 0; i < MomentumArrow.Length; i++)
            arrow[i] = center + MomentumArrow[i].Rotated(turn) * k;
        arrow[^1] = arrow[0];

        // The arrow bends round the ring, so fill it as a fan from its tip.
        Span<Vector2> triangle = stackalloc Vector2[3];
        for (int i = 1; i < MomentumArrow.Length - 1; i++)
        {
            triangle[0] = arrow[0];
            triangle[1] = arrow[i];
            triangle[2] = arrow[i + 1];
            canvas.DrawPolygon(rid, triangle, color);
        }
        canvas.DrawPolyline(rid, arrow, edge, 3f * k);

        int chevrons = Math.Min(MomentumLevel(note.Momentum) - 1, MomentumChevrons.Length);
        Span<Vector2> chevron = stackalloc Vector2[3];
        for (int c = 0; c < chevrons; c++)
        {
            for (int i = 0; i < 3; i++)
                chevron[i] = center + MomentumChevrons[c][i].Rotated(turn) * k;
            canvas.DrawPolyline(rid, chevron, edge, 5f * k);
        }
    }

    /// <summary>How strongly a dash note's connector and exit show before the dash.</summary>
    public const float DashLinkAlpha = 0.5f;

    /// <summary>A dash note: a hollow ring with four triangles pointing in, where the player vanishes.</summary>
    private static void DrawDashShape(IRectCanvas canvas, Rid rid, Vector2 center, float radius, Color color) =>
        DrawDashPoint(canvas, rid, center, radius, color, inward: true);

    /// <summary>A dash's exit: the same ring with the triangles pointing out, where the player appears.</summary>
    public static void DrawDashExit(IRectCanvas canvas, Rid rid, Vector2 center, float radius, Color color) =>
        DrawDashPoint(canvas, rid, center, radius, color, inward: false);

    private static void DrawDashPoint(
        IRectCanvas canvas,
        Rid rid,
        Vector2 center,
        float radius,
        Color color,
        bool inward)
    {
        float k = radius / DesignRadius;
        canvas.DrawCircleOutline(rid, center, radius, color, 4f * k);

        // Drawn for the left triangle and turned for the other three.
        float tip = inward ? -9f : -22f;
        float baseX = inward ? -18f : -13f;
        Span<Vector2> triangle = stackalloc Vector2[3];
        for (int i = 0; i < 4; i++)
        {
            float turn = i * Mathf.Pi / 2;
            triangle[0] = center + new Vector2(tip, 0f).Rotated(turn) * k;
            triangle[1] = center + new Vector2(baseX, -9f).Rotated(turn) * k;
            triangle[2] = center + new Vector2(baseX, 9f).Rotated(turn) * k;
            canvas.DrawPolygon(rid, triangle, color);
        }
    }

    /// <summary>Where a dash note's exit is drawn, in screen space.</summary>
    public static Vector2 DashExitScreen(Note note, PlayfieldView view) =>
        view.PlayfieldToScreen(
            PlayerMovementSystem.DashTarget(note.Position, note.DashDirection, note.DashStrength));

    /// <summary>
    /// Where a dash note sends the player: the exit, joined to the note by a
    /// jagged connector, both at half strength so the note itself reads as the
    /// one to hit. Strength and direction need no marks of their own.
    /// </summary>
    public static void DrawDashLink(
        IRectCanvas canvas,
        Rid rid,
        Note note,
        PlayfieldView view,
        float radius,
        Color color)
    {
        Vector2 from = view.PlayfieldToScreen(note.Position);
        Vector2 to = DashExitScreen(note, view);
        Color faded = Tinted(color, DashLinkAlpha);
        DrawDashConnector(canvas, rid, from, to, radius, faded);
        DrawDashExit(canvas, rid, to, radius, faded);
    }

    /// <summary>The jagged connector between a dash note and its exit, kept clear of both rings.</summary>
    public static void DrawDashConnector(
        IRectCanvas canvas,
        Rid rid,
        Vector2 from,
        Vector2 to,
        float radius,
        Color color)
    {
        float k = radius / DesignRadius;
        float gap = radius + 6f * k;
        Vector2 along = to - from;
        float length = along.Length();
        if (length <= gap * 2)
            return;

        Vector2 direction = along / length;
        Vector2 side = new(-direction.Y, direction.X);
        Vector2 start = from + direction * gap;
        Vector2 end = to - direction * gap;
        float jag = 9f * k;
        Span<Vector2> bolt =
        [
            start,
            start.Lerp(end, 0.33f) + side * jag,
            start.Lerp(end, 0.72f) - side * jag,
            end,
        ];
        canvas.DrawPolyline(rid, bolt, color, 5f * k);

        // A short stroke beside each leg, on the outside of its bend.
        for (int i = 0; i < 3; i++)
        {
            Vector2 a = bolt[i];
            Vector2 b = bolt[i + 1];
            Vector2 leg = (b - a).Normalized();
            Vector2 outside = new Vector2(-leg.Y, leg.X) * (i % 2 == 0 ? 1f : -1f) * 10f * k;
            canvas.DrawLine(rid, a.Lerp(b, 0.3f) + outside, a.Lerp(b, 0.7f) + outside, color, 4f * k);
        }
    }

    /// <summary>The filled ring every solid note sits in.</summary>
    private static void DrawRing(IRectCanvas canvas, Rid rid, Vector2 center, float radius, Color color, float k)
    {
        canvas.DrawCircle(rid, center, radius, color);
        canvas.DrawCircleOutline(rid, center, radius, Tinted(NoteEdgeColor, color.A), 4f * k);
    }

    /// <summary>One, two or three steps of push, matching the editor's 1×/2×/3× momentum buttons.</summary>
    public static int MomentumLevel(float momentum) =>
        momentum >= 1050f ? 3 : momentum >= 750f ? 2 : 1;

    private static Color Tinted(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
}
