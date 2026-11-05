using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws start points, which are the size of a note so they read at any zoom.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>A start point's radius on screen: a note's.</summary>
    public static float StartPointRadius(PlayfieldView view) => NoteSize * view.Scale / 2f;

    /// <summary>A start point's ring and "S", at <paramref name="radius"/> around <paramref name="center"/>.</summary>
    public static void DrawStartPointBody(IRectCanvas canvas, Rid rid, Vector2 center, float radius, Color color)
    {
        canvas.DrawCircleOutline(rid, center, radius, color, 2f);
        int fontSize = Math.Max(1, (int)MathF.Round(radius * 0.9f));
        var box = new Vector2(radius * 2, radius * 2);
        canvas.DrawTextStyled(rid, new Rect2(center - box / 2, box), "S", color, fontSize);
    }

    /// <summary>One start point, ringed when selected and badged when grouped, the way a note is.</summary>
    public static void DrawStartPoint(IRectCanvas canvas, Rid rid, Entity entity, PlayfieldView view, bool selected)
    {
        StartPoint start = entity.GetComponent<StartPoint>();
        Vector2 center = view.PlayfieldToScreen(start.Position);
        float radius = StartPointRadius(view);
        SelectItemShader(canvas, entity);
        DrawStartPointBody(canvas, rid, center, radius, StartPointColor);

        if (selected)
            canvas.DrawCircleOutline(rid, center, radius + 4f, SelectedNoteColor, 2);

        canvas.SelectShader(0);
    }
}
