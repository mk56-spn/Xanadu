using Box2DSharp.Dynamics;
using NUnit.Framework;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

/// <summary>An aimed drop lands the player on the first note at its time.</summary>
[TestFixture]
public sealed class PlayerDropInTests
{
    [TestCase(60)]
    [TestCase(144)]
    public void TheDropArrivesOnTheNoteAtItsTime(int rate)
    {
        using var physics = new Box2DPhysicsWorld();
        var note = new Vector2(500f, 600f);
        Vector2 spawn = note - new Vector2(0f, PlayerDropIn.Fallen(PlayerDropIn.EntrySpeed, PlayerDropIn.DropSeconds));
        Body body = physics.CreateDynamicBox(spawn, new Vector2(30, 100));
        body.SetLinearVelocity(new Vector2(0f, PlayerDropIn.EntrySpeed));

        int frames = (int)System.MathF.Round(PlayerDropIn.DropSeconds * rate);
        for (int i = 0; i < frames; i++)
            physics.Step(1f / rate);

        float arrived = Box2DPhysicsWorld.ToScreen(body.GetPosition()).Y;
        Assert.That(arrived, Is.EqualTo(note.Y).Within(NoteReach.Radius / 4));
    }
}
