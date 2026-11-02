using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Box2D body attached to an ECS entity.</summary>
public struct PhysicsBody(Box2DSharp.Dynamics.Body body) : IComponent
{
    public Box2DSharp.Dynamics.Body Body = body;
}

/// <summary>
/// Where a player is drawn. Physics moves it in fixed steps, slower than frames
/// on a fast display, so drawing <see cref="Position"/> directly would stutter
/// against everything that moves every frame. This eases from the position
/// before the last step to the one after it, one step behind the simulation.
/// </summary>
public struct PlayerSmoothing(Vector2 position) : IComponent
{
    /// <summary>The position before the last physics step.</summary>
    public Vector2 Previous = position;

    /// <summary>The position that step reached.</summary>
    public Vector2 Stepped = position;

    /// <summary>How far through the next step the clock is, from 0 to 1.</summary>
    public float Alpha;

    /// <summary>The position to draw this frame.</summary>
    public Vector2 Drawn = position;

    /// <summary>Where to draw a player: its logical position until it has a body to ease.</summary>
    public static Vector2 DrawnPosition(Entity player) =>
        player.HasComponent<PhysicsBody>() && player.TryGetComponent(out PlayerSmoothing smoothing)
            ? smoothing.Drawn
            : player.GetComponent<Position>().Value;
}

/// <summary>Marks an entity as a character with a mass in kilograms.</summary>
public struct Character(float mass) : IComponent
{
    public float Mass = mass;
}

/// <summary>Linear momentum in kg*m/s.</summary>
public struct Momentum(System.Numerics.Vector2 value) : IComponent
{
    public System.Numerics.Vector2 Value = value;
}
