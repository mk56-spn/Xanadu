using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

/// <summary>Draws friction bars in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>One friction bar as a plain orange bar; a selected one gets an accent edge.</summary>
    public static void DrawFrictionBar(IRectCanvas canvas, Handle rid, Entity entity, PlayfieldView view, bool selected)
    {
        FrictionBar bar = entity.GetComponent<FrictionBar>();
        if (bar.Height <= 0)
            return;

        SelectItemShader(canvas, entity);
        Rect2 screen = FrictionBarScreenBounds(bar, GroupOffset.View(entity, view));
        canvas.DrawRect(rid, screen, GroupColor.For(entity, FrictionBarColor));
        if (selected)
        {
            Vector2 a = screen.Position;
            Vector2 b = screen.End;
            System.Span<Vector2> edge = [a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y), a];
            canvas.DrawPolyline(rid, edge, BlockSelectedEdge, SelectedEdgeThickness);
        }

        canvas.SelectShader(0);
    }

}
