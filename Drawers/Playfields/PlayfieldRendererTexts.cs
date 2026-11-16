using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

/// <summary>Draws the text written into a level, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>
    /// Draws one level text centred on its position, at its size scaled by the
    /// view. Empty text draws nothing unless it is selected, so a cleared label
    /// can still be found; a selected one gets an accent box.
    /// </summary>
    public static void DrawLevelText(
        IRectCanvas canvas,
        Handle rid,
        Entity entity,
        PlayfieldView view,
        bool selected)
    {
        LevelText text = entity.GetComponent<LevelText>();
        Rect2 bounds = TextScreenBounds(text, view);

        if (!string.IsNullOrEmpty(text.Text))
        {
            SelectItemShader(canvas, entity);
            int fontSize = Math.Max(1, (int)MathF.Round(text.Size * view.Scale));
            canvas.DrawTextTurned(rid, DrawArea(bounds), text.RotationDegrees, text.Text, GroupColor.For(entity, TextColor), fontSize);
            canvas.SelectShader(0);
        }

        if (selected)
            DrawTurnedBorder(canvas, rid, text.Bounds, text.RotationDegrees, view);
    }

    /// <summary>
    /// Where a text is drawn: its box widened on both sides, since the box is
    /// only an estimate and drawn text is clipped to the area it is given.
    /// Centred text lands in the same place either way.
    /// </summary>
    public static Rect2 DrawArea(Rect2 bounds) => bounds.GrowIndividual(bounds.Size.X, 0, bounds.Size.X, 0);

}
