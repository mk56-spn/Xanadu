using Friflo.Engine.ECS;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A hit dash note's run, on the note: the way it was hit. At the note's time
/// the body is teleported from the note that way by the note's length, then
/// keeps the velocity it had (<see cref="Applied"/> once done).
/// </summary>
public struct DashRun : IComponent
{
    public RouteHeading Heading;

    public bool Applied;
}

/// <summary>A hold note whose head was hit, on the note: the key that has to stay down until the tail.</summary>
public struct HoldRun : IComponent
{
    public KeybindAction Key;
}
