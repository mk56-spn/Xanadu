using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A point the pointer can take and drag: a press within <see cref="Reach"/> pixels of the
/// entity's centre takes it, and where several are in reach the one with the least reach wins,
/// so a dot on a shape beats the shape's body. While held it carries <see cref="Pressed"/> (and
/// <see cref="Hovered"/> while the pointer could take it) and hears <see cref="GripTaken"/>,
/// <see cref="GripPulled"/> and <see cref="GripLetGo"/>. Its owner moves it; it never moves itself.
/// Drawn as a <see cref="GripDot"/> or a <see cref="GripSquare"/>, or not at all with neither.
/// </summary>
public struct UiGrip(float reach) : IComponent
{
    public float Reach = reach;
}

/// <summary>A grip drawn as a round dot.</summary>
public struct GripDot : ITag;

/// <summary>A grip drawn as a small square.</summary>
public struct GripSquare : ITag;

/// <summary>A press took the grip, the pointer at <paramref name="Pointer"/> (stage pixels).</summary>
public readonly record struct GripTaken(Vector2 Pointer);

/// <summary>The held grip's pointer moved to <paramref name="Pointer"/>.</summary>
public readonly record struct GripPulled(Vector2 Pointer);

/// <summary>The pointer let the grip go.</summary>
public readonly record struct GripLetGo;
