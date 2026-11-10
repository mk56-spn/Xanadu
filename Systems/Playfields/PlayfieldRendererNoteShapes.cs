using System;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The action, momentum and dash note shapes from the note designs. They are
/// authored on a 36-unit radius and scaled to the note: the note colour fills
/// the frame, white is the line work and the core is dark. Directional notes
/// show their strength as pips, never a way; a diagonal one is a rhombus.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color NoteEdgeColor = Palette.TextBright;
    public static readonly Color NoteCoreColor = Palette.SurfaceAlt;

    /// <summary>The radius the note designs are drawn at.</summary>
    private const float DesignRadius = 36f;

    /// <summary>A keynote: a filled ring around a dark core.</summary>
    private static void DrawHitShape(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        DrawRing(canvas, rid, center, radius, color, k);
        canvas.DrawCircle(rid, center, 20f * k, Tinted(NoteCoreColor, color.A));
        canvas.DrawCircleOutline(rid, center, 20f * k, Tinted(NoteEdgeColor, color.A), 5f * k);
    }

    /// <summary>A neutral action note: a hollow ring around a dark core, so it reads as having no colour of its own.</summary>
    private static void DrawNeutralShape(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        canvas.DrawCircleOutline(rid, center, radius - 2f * k, color, 6f * k);
        canvas.DrawCircle(rid, center, 20f * k, Tinted(NoteCoreColor, color.A));
        canvas.DrawCircleOutline(rid, center, 20f * k, color, 4f * k);
    }

    /// <summary>
    /// A Contact barrier: a ring with one row of pips per bar it asks of, each
    /// in its bar's colour, one pip per point of cost.
    /// </summary>
    private static void DrawBarrierShape(IRectCanvas canvas, Handle rid, Note note, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        canvas.DrawCircle(rid, center, radius, Tinted(NoteCoreColor, color.A));
        canvas.DrawCircleOutline(rid, center, radius, color, 5f * k);

        int rows = note.Costs.UsedBars;
        float rowGap = 14f * k;
        float y = -(rows - 1) * rowGap / 2;
        for (int bar = 0; bar < ContactCounters.Bars; bar++)
        {
            int cost = note.Costs[bar];
            if (cost <= 0)
                continue;

            Color pip = Tinted(BarColor(bar), color.A);
            float gap = 11f * k;
            float start = -(cost - 1) * gap / 2;
            for (int i = 0; i < cost; i++)
                canvas.DrawCircle(rid, center + new Vector2(start + i * gap, y), 4.5f * k, pip);
            y += rowGap;
        }
    }

    /// <summary>
    /// A momentum note: the frame around a dark hexagon, with one pip per step
    /// of strength. It shows no way: the player picks it.
    /// </summary>
    private static void DrawMomentumShape(IRectCanvas canvas, Handle rid, Note note, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        Color edge = Tinted(NoteEdgeColor, color.A);
        DrawFrame(canvas, rid, center, radius, color, k, note.Diagonal);

        Span<Vector2> hexagon = stackalloc Vector2[7];
        for (int i = 0; i < 6; i++)
        {
            float angle = -Mathf.Pi / 2 + i * Mathf.Tau / 6;
            hexagon[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22.56f * k;
        }
        hexagon[6] = hexagon[0];
        canvas.DrawPolygon(rid, hexagon[..6], Tinted(NoteCoreColor, color.A));
        canvas.DrawPolyline(rid, hexagon, edge, 4f * k);
        DrawPips(canvas, rid, center, k, MomentumLevel(note.Momentum), edge);
    }

    /// <summary>
    /// A dash note: a hollow frame with four triangles pointing in, where the
    /// player vanishes, and one pip per dash length.
    /// </summary>
    private static void DrawDashShape(IRectCanvas canvas, Handle rid, Note note, Vector2 center, float radius, Color color)
    {
        float k = radius / DesignRadius;
        if (note.Diagonal)
            canvas.DrawPolyline(rid, Rhombus(center, radius), color, 4f * k);
        else
            canvas.DrawCircleOutline(rid, center, radius, color, 4f * k);

        // Drawn for the left triangle and turned for the other three.
        Span<Vector2> triangle = stackalloc Vector2[3];
        for (int i = 0; i < 4; i++)
        {
            float turn = i * Mathf.Pi / 2;
            triangle[0] = center + new Vector2(-9f, 0f).Rotated(turn) * k;
            triangle[1] = center + new Vector2(-18f, -9f).Rotated(turn) * k;
            triangle[2] = center + new Vector2(-18f, 9f).Rotated(turn) * k;
            canvas.DrawPolygon(rid, triangle, color);
        }

        DrawPips(canvas, rid, center + new Vector2(0f, 20f * k), k, (int)note.DashStrength + 1, color);
    }

    /// <summary>A row of small dots, one per step of strength, centred under <paramref name="center"/>.</summary>
    private static void DrawPips(IRectCanvas canvas, Handle rid, Vector2 center, float k, int count, Color color)
    {
        float gap = 9f * k;
        float start = -(count - 1) * gap / 2;
        for (int i = 0; i < count; i++)
            canvas.DrawCircle(rid, center + new Vector2(start + i * gap, 9f * k), 3f * k, color);
    }

    /// <summary>The four corners of a rhombus note's frame, closed, with its points up, right, down and left.</summary>
    private static Vector2[] Rhombus(Vector2 center, float radius)
    {
        float r = radius * 1.15f;
        return
        [
            center + new Vector2(0, -r),
            center + new Vector2(r, 0),
            center + new Vector2(0, r),
            center + new Vector2(-r, 0),
            center + new Vector2(0, -r),
        ];
    }

    /// <summary>The filled frame a solid note sits in: a ring, or a rhombus for a diagonal note.</summary>
    private static void DrawFrame(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color, float k, bool diagonal)
    {
        if (!diagonal)
        {
            DrawRing(canvas, rid, center, radius, color, k);
            return;
        }

        Vector2[] corners = Rhombus(center, radius);
        canvas.DrawPolygon(rid, corners.AsSpan(0, 4), color);
        canvas.DrawPolyline(rid, corners, Tinted(NoteEdgeColor, color.A), 4f * k);
    }

    /// <summary>The filled ring every solid note sits in.</summary>
    private static void DrawRing(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color, float k)
    {
        canvas.DrawCircle(rid, center, radius, color);
        canvas.DrawCircleOutline(rid, center, radius, Tinted(NoteEdgeColor, color.A), 4f * k);
    }

    /// <summary>One, two or three steps of push, matching the editor's 1×/2×/3× momentum buttons.</summary>
    public static int MomentumLevel(float momentum) =>
        momentum >= 1050f ? 3 : momentum >= 750f ? 2 : 1;

    private static Color Tinted(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
}
