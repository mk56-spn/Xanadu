using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using Position = Xanadu.Core.Position;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>The player: the hovering blue body, 1 wide by 2.5 tall, where the curve player put it, leaning toward where it heads (<see cref="PlayRules.Tilt"/>) and bobbing slightly (<see cref="PlayRules.Bob"/>).</summary>
    public static void DrawPlayer(
        IRectCanvas canvas,
        Handle rid,
        EntityStore entities,
        PlayfieldView view)
    {
        // One player at most.
        foreach (Entity entity in entities.Query<Player, Position>().ToEntityList())
        {
            Vector2 center = entity.GetComponent<Position>().Value;
            if (entity.TryGetComponent(out PlayCamera camera))
                center += camera.Subpixel;

            Vector2 half = PlayRules.BodySize * view.Scale / 2;
            if (entity.TryGetComponent(out BodyTrace trace))
                DrawBodyTrace(canvas, rid, trace.Points, view, center);

            bool moved = entity.TryGetComponent(out CurveBody curveBody);
            float alpha = moved ? curveBody.Alpha : 1f;
            if (alpha <= 0f)
                continue;
            if (moved)
                center.Y += PlayRules.Bob(curveBody.SongMs) * view.Scale;

            float tilt = moved ? PlayRules.Tilt(curveBody.Velocity) : 0f;
            float cos = MathF.Cos(tilt);
            float sin = MathF.Sin(tilt);
            Span<Vector2> corners = stackalloc Vector2[5];
            corners[0] = center + Turn(-half.X, -half.Y);
            corners[1] = center + Turn(half.X, -half.Y);
            corners[2] = center + Turn(half.X, half.Y);
            corners[3] = center + Turn(-half.X, half.Y);
            corners[4] = corners[0];
            canvas.DrawPolygon(rid, corners[..4], Faded(BodyFill, alpha));
            canvas.DrawPolyline(rid, corners, Faded(BodyEdge, alpha), 2);

            Vector2 Turn(float x, float y) => new(x * cos - y * sin, x * sin + y * cos);
            static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);
        }
    }

    /// <summary>
    /// The line the body's centre has run along: in an editor preview up to where it is now
    /// (<paramref name="center"/>, shifted like the body), and back in the editor the last preview's whole line.
    /// </summary>
    public static void DrawBodyTrace(IRectCanvas canvas, Handle rid, List<Vector2>? points, PlayfieldView view, Vector2? center = null)
    {
        if (points is null || points.Count + (center is null ? 0 : 1) < 2)
            return;

        var screen = new Vector2[points.Count + (center is null ? 0 : 1)];
        for (int i = 0; i < points.Count; i++)
            screen[i] = view.PlayfieldToScreen(points[i]);
        if (center is Vector2 end)
            screen[^1] = end;
        Color edge = BodyEdge;
        canvas.DrawPolyline(rid, screen, new Color(edge.R, edge.G, edge.B, edge.A * 0.6f), 2);
    }
}
