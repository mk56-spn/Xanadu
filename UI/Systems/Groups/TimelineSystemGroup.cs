using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Input;
using Xanadu.Systems.Timeline;

namespace Xanadu.Systems.Groups;

/// <summary>
/// Behaviour every timeline shares, whichever tool owns it. Each tool adds this
/// group to its own and describes its timeline with a <see cref="TimelineHost"/>.
/// Keys go through <see cref="TimelineShortcuts"/> from each tool's dispatcher,
/// which knows when its keys are live.
/// </summary>
public sealed class TimelineSystemGroup : SystemGroup
{
    public TimelineSystemGroup(IPointerInputSource pointer, IWheelSource wheel) : base("Timeline")
    {
        Add(new TimelineWheelSystem(pointer, wheel));
        Add(new TimelinePinSystem());
    }
}
