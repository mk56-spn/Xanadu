using System;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator;

/// <summary>Draws each override as a bar across its time on its bone's override row, with grips to drag at its ends.</summary>
public static class AnimatorOverrideBars
{
    private const float Inset = 2f;
    private const float GripWidth = 4f;
    private const int FontSize = 11;

    public static void Draw(IRectCanvas canvas, Rid target, AnimatorTimelineModel model, XafLimbTarget? selected)
    {
        for (int screenRow = 0; screenRow < model.LastRow - model.FirstRow; screenRow++)
        {
            AnimatorTrackRow track = model.Tracks[model.FirstRow + screenRow];
            if (!track.IsOverride)
                continue;

            Rect2 row = model.RowRect(screenRow);
            foreach (XafLimbTarget item in model.Clip.Targets)
            {
                if (item.BoneId == track.Id)
                    DrawBar(canvas, target, model, row, item, ReferenceEquals(item, selected));
            }
        }
    }

    private static void DrawBar(
        IRectCanvas canvas,
        Rid target,
        AnimatorTimelineModel model,
        Rect2 row,
        XafLimbTarget item,
        bool selected)
    {
        float left = Math.Max(model.XForSeconds(item.From), model.Ruler.Position.X);
        float right = Math.Min(model.XForSeconds(item.To), model.Ruler.End.X);
        if (right <= left)
            return;

        Color color = item.Shape == XafTargetShape.Spot ? Palette.Lime : Palette.Violet;
        var bar = new Rect2(left, row.Position.Y + Inset, right - left, row.Size.Y - Inset * 2);
        canvas.DrawRect(target, bar, new Color(color, selected ? 0.55f : 0.3f));
        canvas.DrawRect(target, new Rect2(bar.Position, new Vector2(GripWidth, bar.Size.Y)), color);
        canvas.DrawRect(target, new Rect2(bar.End.X - GripWidth, bar.Position.Y, GripWidth, bar.Size.Y), color);
        if (bar.Size.X > GripWidth * 2 + 40f)
        {
            canvas.DrawTextStyledLeft(
                target,
                new Rect2(bar.Position.X + GripWidth + 4f, row.Position.Y + 2f, bar.Size.X - GripWidth * 2 - 8f, row.Size.Y - 4f),
                $"{AnimatorOverrideCommands.Name(item)}  {item.To - item.From:0.00}s",
                Palette.Text,
                FontSize);
        }
    }
}
