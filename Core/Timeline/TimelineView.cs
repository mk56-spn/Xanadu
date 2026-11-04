using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Horizontal zoom of a timeline view, plus the visible window captured while
/// the playhead is dragged so the view does not slide under the pointer.
/// </summary>
public struct TimelineView(float zoom) : IComponent
{
    public float Zoom = zoom;
    public bool AutoZoomed;

    /// <summary>
    /// The window is held at <see cref="PinStartMs"/> instead of following the
    /// playhead: a wheel zoom pins it so the time under the pointer stays put.
    /// It lets go once the playhead moves out of it.
    /// </summary>
    public bool Pinned;
    public long PinStartMs;

    /// <summary>The playhead when the pin was last checked, to tell when it moves.</summary>
    public long PinNowMs;

    /// <summary>Visible window captured while the playhead is being dragged.</summary>
    public bool Scrubbing;
    public long ScrubStartMs;
    public long ScrubEndMs;
}
