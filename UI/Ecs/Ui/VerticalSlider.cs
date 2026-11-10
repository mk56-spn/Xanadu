
namespace Xanadu.Ecs;

/// <summary>
/// A vertical slider whose thumb shows how much of a list is on screen, which is
/// all a scrollbar is. It only does the sums: value runs from 0 at the top to 1
/// at the bottom, and each host draws the thumb and owns what the value means.
/// </summary>
/// <param name="Top">The top of the track.</param>
/// <param name="Bottom">The bottom of the track.</param>
/// <param name="Shown">The fraction of the list on screen; the thumb's share of the track.</param>
/// <param name="MinThumb">The shortest the thumb gets, so a long list can still be grabbed.</param>
public readonly record struct VerticalSlider(float Top, float Bottom, float Shown, float MinThumb)
{
    public float ThumbLength => Mathf.Clamp((Bottom - Top) * Shown, Mathf.Min(MinThumb, Bottom - Top), Bottom - Top);

    /// <summary>How far the thumb's top can move.</summary>
    public float Travel => Bottom - Top - ThumbLength;

    /// <summary>Whether there is anywhere to slide to.</summary>
    public bool CanSlide => Travel > 0f;

    public float ThumbTop(float value) => Top + Travel * Mathf.Clamp(value, 0f, 1f);

    /// <summary>
    /// Where along the thumb a press takes hold. A press on the thumb keeps its
    /// place so nothing jumps; one on the bare track centres the thumb on the pointer.
    /// </summary>
    public float Grab(float pointerY, float value)
    {
        float along = pointerY - ThumbTop(value);
        return along >= 0f && along <= ThumbLength ? along : ThumbLength / 2f;
    }

    /// <summary>The value that puts the grabbed point of the thumb under the pointer.</summary>
    public float ValueAt(float pointerY, float grab) =>
        CanSlide ? Mathf.Clamp((pointerY - grab - Top) / Travel, 0f, 1f) : 0f;
}
