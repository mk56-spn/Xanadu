using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Ui.Systems.Groups;

/// <summary>
/// Behaviour every timeline shares, whichever tool owns it. Each tool adds this
/// group to its own, gives its timeline entity a <see cref="TimelineFrame"/> and
/// writes it on <see cref="TimelineFrameDue"/>; presses and drags come back as
/// the timeline signals (<c>TimelineSeek</c>, <c>TimelineItemMoved</c>, …).
/// Keys go through <see cref="TimelineShortcuts"/> from each tool's dispatcher,
/// which knows when its keys are live.
/// </summary>
public sealed class TimelineSystemGroup : SystemGroup
{
    public TimelineSystemGroup(IPointerInputSource pointer, IWheelSource wheel) : base("Timeline")
    {
        Add(new TimelineFrameSystem());
        Add(new TimelineWheelSystem(pointer, wheel));
        Add(new TimelinePinSystem());
        Add(new TimelineInputSystem(pointer, wheel));
    }
}
