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

    /// <summary>Visible window captured while the playhead is being dragged.</summary>
    public bool Scrubbing;
    public long ScrubStartMs;
    public long ScrubEndMs;
}
