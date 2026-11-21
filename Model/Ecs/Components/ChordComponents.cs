using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A chord: a set of diagonal lines, one per action key, that notes fall down
/// to show what's coming. It sits still in the level from its time until the
/// next chord's. <see cref="Anchor"/> is where its first line meets the
/// judgement line, so its height is the chord's impact height
/// (<see cref="Playfields.Chords"/> has the shape).
/// </summary>
public struct Chord(long timeMs, Vector2 anchor) : IComponent
{
    public long TimeMs = timeMs;

    public Vector2 Anchor = anchor;
}
