using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws the text written into a level, in both editor and play modes.</summary>
public static partial class PlayfieldRenderer
{
    /// <summary>Text has no colour of its own: white shows exactly what its self modulate says.</summary>
    public static readonly Color TextColor = Colors.White;

    /// <summary>
    /// Draws every level text centred on its position, at its size scaled by the
    /// view. Empty text draws nothing unless it is selected, so a cleared label
    /// can still be found; a selected one gets an accent box.
    /// </summary>
    public static void DrawTexts(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldView view,
        Func<Entity, bool>? isSelected = null)
    {
        foreach (Entity entity in entities.Query<LevelText>().ToEntityList())
        {
            LevelText text = entity.GetComponent<LevelText>();
            Rect2 bounds = TextScreenBounds(text, view);
            bool selected = isSelected?.Invoke(entity) == true;

            if (!string.IsNullOrEmpty(text.Text))
            {
                canvas.SelectShader(ItemShader.Of(entity));
                int fontSize = Math.Max(1, (int)MathF.Round(text.Size * view.Scale));
                canvas.DrawTextTurned(rid, DrawArea(bounds), text.RotationDegrees, text.Text, GroupColor.For(entity, TextColor), fontSize);
                canvas.SelectShader(0);
            }

            if (selected)
                DrawTurnedBorder(canvas, rid, text.Bounds, text.RotationDegrees, view);
        }
    }

    /// <summary>
    /// Where a text is drawn: its box widened on both sides, since the box is
    /// only an estimate and drawn text is clipped to the area it is given.
    /// Centred text lands in the same place either way.
    /// </summary>
    public static Rect2 DrawArea(Rect2 bounds) => bounds.GrowIndividual(bounds.Size.X, 0, bounds.Size.X, 0);

    /// <summary>The box a level text roughly fills before it is turned, on screen.</summary>
    public static Rect2 TextScreenBounds(LevelText text, PlayfieldView view)
    {
        Rect2 bounds = text.Bounds;
        return new Rect2(view.PlayfieldToScreen(bounds.Position), bounds.Size * view.Scale);
    }

    /// <summary>The upright box around a level text as it is turned, on screen, for box selection.</summary>
    public static Rect2 TextScreenHull(LevelText text, PlayfieldView view)
    {
        Rect2 bounds = TurnedBox.Hull(text.Bounds, text.RotationDegrees);
        return new Rect2(view.PlayfieldToScreen(bounds.Position), bounds.Size * view.Scale);
    }
}
