using Xanadu.Core.Maths;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A slider whose thumb shows how much of a list or a span is on screen, which
/// is all a scrollbar is. It runs along one axis, down or across, and only
/// does the sums: value runs from 0 at <see cref="Start"/> to 1 at
/// <see cref="End"/>, and each host draws the thumb and owns what the value means.
/// </summary>
/// <param name="Start">The track's top, or its left end.</param>
/// <param name="End">The track's bottom, or its right end.</param>
/// <param name="Shown">The fraction on screen; the thumb's share of the track.</param>
/// <param name="MinThumb">The shortest the thumb gets, so a long list can still be grabbed.</param>
public readonly record struct ScrollSlider(float Start, float End, float Shown, float MinThumb)
{
    public float ThumbLength => Math.Clamp((End - Start) * Shown, Math.Min(MinThumb, End - Start), End - Start);

    /// <summary>How far the thumb's start can move.</summary>
    public float Travel => End - Start - ThumbLength;

    /// <summary>Whether there is anywhere to slide to.</summary>
    public bool CanSlide => Travel > 0f;

    public float ThumbStart(float value) => Start + Travel * Math.Clamp(value, 0f, 1f);

    /// <summary>
    /// Where along the thumb a press takes hold. A press on the thumb keeps its
    /// place so nothing jumps; one on the bare track centres the thumb on the pointer.
    /// </summary>
    public float Grab(float pointer, float value)
    {
        float along = pointer - ThumbStart(value);
        return along >= 0f && along <= ThumbLength ? along : ThumbLength / 2f;
    }

    /// <summary>The value that puts the grabbed point of the thumb under the pointer.</summary>
    public float ValueAt(float pointer, float grab) =>
        CanSlide ? Math.Clamp((pointer - grab - Start) / Travel, 0f, 1f) : 0f;
}
