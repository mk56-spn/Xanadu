using System.Collections.Generic;
using Godot;
using Xanadu.Animation;
using Xanadu.Timeline;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The animator's band between two keys on a timeline row, built on the shared
/// <see cref="TimelineEaseBand"/>. Its thickness follows how
/// fast the value travels, so it reads along time rather than up the row: an ease
/// in is a wedge that widens, an ease out one that narrows, a plain ease a lens.
/// Overshoot and bounce, where the value runs backwards, come out as their own
/// pieces. A step is a hairline that ends in a full-height tick at the jump.
/// Spans where nothing changes, spans too narrow to read a shape in and spans
/// whose channels ease differently are a hairline through the row's middle.
/// </summary>
public static class AnimatorEaseBand
{
    /// <summary>Space kept above and below the band inside the row.</summary>
    public const float RowPadding = TimelineEaseBand.RowPadding;

    /// <summary>Narrower than this a shape is unreadable, as in a baked run of keys.</summary>
    public const float MinShapeWidth = TimelineEaseBand.MinShapeWidth;

    /// <summary>Half the thickness of a hairline, and of the band where it is still.</summary>
    public const float HairHalf = TimelineEaseBand.HairHalf;

    /// <summary>Width of the tick a step jumps with.</summary>
    public const float StepTickWidth = 2f;

    /// <param name="left">Where the visible timeline starts; nothing is drawn past it.</param>
    /// <param name="right">Where the visible timeline ends.</param>
    public static List<EaseBandPiece> Pieces(
        XafEaseSegment segment, float fromX, float toX, float left, float right, Rect2 row)
    {
        float middle = row.Position.Y + row.Size.Y / 2f;
        var pieces = new List<EaseBandPiece>();
        float start = Mathf.Max(fromX, left);
        float end = Mathf.Min(toX, right);
        if (end <= start)
            return pieces;

        if (segment.Preset is not { } preset || toX - fromX < MinShapeWidth)
        {
            pieces.Add(new(TimelineEaseBand.Bar(start, end, middle, HairHalf), false));
            return pieces;
        }

        if (preset == XafEasePreset.Step)
        {
            // Holds the old value right up to the next key, then jumps.
            float tickLeft = Mathf.Max(toX - StepTickWidth, start);
            if (tickLeft > start)
                pieces.Add(new(TimelineEaseBand.Bar(start, tickLeft, middle, HairHalf), false));
            if (end > tickLeft)
                pieces.Add(new(TimelineEaseBand.Bar(tickLeft, end, middle, row.Size.Y / 2f - 1f), false));
            return pieces;
        }

        return TimelineEaseBand.Pieces(t => XafEasing.Progress(preset, t), fromX, toX, left, right, row);
    }

    /// <summary>How fast the preset moves at a share of the span, 1 being linear; negative where it runs backwards.</summary>
    public static float Speed(XafEasePreset preset, float t) =>
        TimelineEaseBand.Speed(u => XafEasing.Progress(preset, u), t);
}
