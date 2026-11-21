using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// An updraft emitter, freely placed, one fixed diameter wide: in the column
/// above it, gravity gives way to a pull toward <see cref="Height"/> above the
/// emitter, so low-input stretches don't end in a landing. The pull settles
/// every way in to the same hover, so it corrects error instead of carrying it
/// (<see cref="Playfields.Drafts"/> has the shape).
/// </summary>
public struct Draft(Vector2 position, float height) : IComponent
{
    /// <summary>The emitter's centre, playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>How far above the emitter it holds the body's centre, playfield units.</summary>
    public float Height = height;
}
