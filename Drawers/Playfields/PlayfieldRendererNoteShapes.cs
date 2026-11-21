using System;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// The action note shape from the note designs, authored on a 36-unit radius
/// and scaled to the note: the note colour fills the frame, white is the line
/// work and the core is dark.
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

    /// <summary>The filled ring every solid note sits in.</summary>
    private static void DrawRing(IRectCanvas canvas, Handle rid, Vector2 center, float radius, Color color, float k)
    {
        canvas.DrawCircle(rid, center, radius, color);
        canvas.DrawCircleOutline(rid, center, radius, Tinted(NoteEdgeColor, color.A), 4f * k);
    }

    private static Color Tinted(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
}
