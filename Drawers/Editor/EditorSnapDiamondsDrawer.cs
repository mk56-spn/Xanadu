using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs;

namespace Xanadu.Drawers.Editor;

/// <summary>
/// Our take on osu!'s distance snap rings. Routes only run along the axes at one
/// speed, so the places the body can have got to by a snap step after a corner
/// lie inside a diamond (a rhombus) of speed × time; its edge is reached by never
/// doubling back. One diamond per snap step round the latest corner at or before
/// the playhead (see <see cref="TryAnchor"/>), the playhead's own one brighter.
/// Only while placing, clipped to the field.
/// </summary>
public sealed class EditorSnapDiamondsDrawer(EditorDrawSurface surface)
{
    /// <summary>The most diamonds drawn round one route.</summary>
    private const int MaxDiamonds = 256;

    /// <summary>Diamonds closer than this on screen are thinned out, pixels.</summary>
    private const float MinSpacing = 8f;

    private const float FaintAlpha = 0.16f;
    private const float CurrentAlpha = 0.7f;

    public void Draw(EditorFrame frame, bool placing)
    {
        Entity editor = frame.Entity;
        if (!placing || !TryAnchor(editor, frame.Playhead, out Vector2 head, out long headMs, out float speed))
            return;

        Playhead playhead = frame.Playhead;
        PlayfieldView view = frame.View;
        Rect2 field = PlayfieldMetrics.FieldRect(view, frame.Playfield);
        float reachMax = frame.Playfield.Width + frame.Playfield.Height;
        long now = EditorTiming.Snap(playhead.TimeMs, playhead);

        long firstMs = EditorTiming.Step(headMs, playhead, 1);
        float spacing = speed * (firstMs - headMs) / 1000f * view.Scale;
        int stride = spacing <= 0 ? 1 : Math.Max(1, (int)MathF.Ceiling(MinSpacing / spacing));

        Color color = PlayfieldColors.CurveColor;
        for (int k = 1; k <= MaxDiamonds; k++)
        {
            long at = EditorTiming.Step(headMs, playhead, k);
            float reach = speed * (at - headMs) / 1000f;
            if (reach > reachMax)
                break;

            bool current = at == now;
            if (!current && k % stride != 0)
                continue;
            float alpha = current ? CurrentAlpha : FaintAlpha;
            DrawDiamond(view, field, head, reach, new Color(color.R, color.G, color.B, color.A * alpha), current ? 2f : 1f);
        }
    }

    /// <summary>
    /// Where the diamonds measure from: the latest corner (head, turn or end) at or
    /// before the playhead of the route being laid, else of the latest route started
    /// by then. Each turn is a fresh choice of way, so the reach starts over there.
    /// Corners sit where the editor draws the route (every turn carried straight on).
    /// </summary>
    private bool TryAnchor(Entity editor, Playhead playhead, out Vector2 corner, out long cornerMs, out float speed)
    {
        long now = EditorTiming.Snap(playhead.TimeMs, playhead);
        Note? route = null;
        if (editor.TryGetComponent(out EditorSpanDraft draft) && draft.Active && draft.Mode == EditorPlacementMode.Curve)
        {
            route = new Note(draft.StartMs, draft.Head, route: draft.TimesMs.Count >= NoteRoute.MinTimes
                ? new NoteRoute(draft.Strength, draft.TimesMs.ToArray())
                : null);
            speed = NoteRoute.SpeedOf(draft.Strength);
        }
        else
        {
            speed = 0f;
            foreach (var (notes, _) in surface.Entities.Cached<Note>().Chunks)
            {
                foreach (Note note in notes.Span)
                {
                    if (note.Route is not { } found || note.TimeMs > now || (route is { } best && note.TimeMs <= best.TimeMs))
                        continue;
                    route = note;
                    speed = found.Speed;
                }
            }
        }

        corner = default;
        cornerMs = 0;
        if (route is not { } anchor || speed <= 0f)
            return false;

        corner = anchor.Position;
        cornerMs = anchor.TimeMs;
        if (anchor.Route is null)
            return true;

        RoutePath path = RoutePath.Of(anchor);
        for (int i = 1; i < path.Points.Count && path.TimeAt(i) <= now; i++)
        {
            corner = path.Points[i];
            cornerMs = path.TimeAt(i);
        }

        return true;
    }

    private void DrawDiamond(PlayfieldView view, Rect2 field, Vector2 centre, float reach, Color color, float width)
    {
        Span<Vector2> corners = stackalloc Vector2[]
        {
            view.PlayfieldToScreen(centre + new Vector2(reach, 0f)),
            view.PlayfieldToScreen(centre + new Vector2(0f, reach)),
            view.PlayfieldToScreen(centre - new Vector2(reach, 0f)),
            view.PlayfieldToScreen(centre - new Vector2(0f, reach)),
        };

        for (int i = 0; i < 4; i++)
        {
            Vector2 from = corners[i];
            Vector2 to = corners[(i + 1) % 4];
            if (field.ClipSegment(ref from, ref to))
                surface.Canvas.DrawLine(surface.Target, from, to, color, width);
        }
    }
}
