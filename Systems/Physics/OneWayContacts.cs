using Box2DSharp.Collision.Collider;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;
using Xanadu.Ecs;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Systems.Physics;

/// <summary>Marks a block's fixture as colliding on one side only.</summary>
public sealed record OneWaySurface(BlockOneWay Side, NVector2 HalfSize);

/// <summary>Marks a fixture as a box that one-way blocks can let through.</summary>
public sealed record OneWayMover(NVector2 HalfSize);

/// <summary>
/// Switches off a contact with a one-way block unless the player is entirely on
/// the block's colliding side. It looks only at where the two are, so a dash that
/// teleports into the block is simply let out rather than shoved.
/// </summary>
public sealed class OneWayContacts : IContactListener
{
    /// <summary>How far past the surface, in metres, still counts as on it; contacts settle slightly into it.</summary>
    public const float SurfaceTolerance = 0.02f;

    public void BeginContact(Contact contact)
    {
    }

    public void EndContact(Contact contact)
    {
    }

    public void PreSolve(Contact contact, in Manifold oldManifold)
    {
        Fixture a = contact.FixtureA;
        Fixture b = contact.FixtureB;
        if (a.UserData is OneWaySurface surfaceA && b.UserData is OneWayMover moverB)
        {
            if (!Collides(surfaceA, a.Body.GetPosition(), moverB, b.Body.GetPosition()))
                contact.SetEnabled(false);
        }
        else if (b.UserData is OneWaySurface surfaceB && a.UserData is OneWayMover moverA)
        {
            if (!Collides(surfaceB, b.Body.GetPosition(), moverA, a.Body.GetPosition()))
                contact.SetEnabled(false);
        }
    }

    public void PostSolve(Contact contact, in ContactImpulse impulse)
    {
    }

    /// <summary>Whether the mover is outside the surface's colliding side. Physics coordinates are y-down.</summary>
    public static bool Collides(
        OneWaySurface surface,
        NVector2 surfaceCenter,
        OneWayMover mover,
        NVector2 moverCenter)
    {
        NVector2 gap = surface.HalfSize + mover.HalfSize - new NVector2(SurfaceTolerance);
        return surface.Side switch
        {
            BlockOneWay.Up => surfaceCenter.Y - moverCenter.Y >= gap.Y,
            BlockOneWay.Down => moverCenter.Y - surfaceCenter.Y >= gap.Y,
            BlockOneWay.Left => surfaceCenter.X - moverCenter.X >= gap.X,
            BlockOneWay.Right => moverCenter.X - surfaceCenter.X >= gap.X,
            _ => true,
        };
    }
}
