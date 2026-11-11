using Friflo.Engine.ECS;

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

/// <summary>
/// A rigged player lifts her legs in the air, so her drawn feet rise off the
/// bottom of her box; the body's bottom rises with them, and nothing catches on
/// legs that are not there. Landing on a block still counts from the full box,
/// the way a person's feet reach the ground before their knees would.
/// </summary>
public struct PlayerTuck : IComponent
{
    /// <summary>How far the drawn feet sit above the box's bottom, in screen pixels; the rig's draw measures it each frame.</summary>
    public float Lift;

    /// <summary>How far the body's bottom is raised now, in screen pixels.</summary>
    public float Raised;

    /// <summary>Screen Y of the full box's bottom after the last physics frame.</summary>
    public float LastFeet;

    /// <summary>The box a player centred at <paramref name="centre"/> collides with: its full size, less any raised bottom.</summary>
    public static Rect2 Box(Entity player, Vector2 centre)
    {
        Vector2 size = player.GetComponent<Player>().Size;
        float raised = player.TryGetComponent(out PlayerTuck tuck) ? tuck.Raised : 0f;
        return new Rect2(centre - size / 2, new Vector2(size.X, size.Y - raised));
    }
}

/// <summary>
/// Whether the ground grips the player. It starts off, so a slide keeps its
/// speed and the player stops only where the level says: crossing a
/// <see cref="FrictionBar"/> turns it on, and the next momentum push turns it
/// off again.
/// </summary>
public struct PlayerFriction : IComponent
{
    public bool On;

    /// <summary>Screen X of the player's centre last frame, to tell when a bar is crossed.</summary>
    public float LastX;

    /// <summary>Whether the ground grips <paramref name="player"/> now.</summary>
    public static bool IsOn(Entity player) =>
        player.TryGetComponent(out PlayerFriction friction) && friction.On;

    /// <summary>Lets go of the ground: a momentum push slides until the next bar.</summary>
    public static void Release(Entity player)
    {
        if (player.HasComponent<PlayerFriction>())
            player.GetComponent<PlayerFriction>().On = false;
    }
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
