using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class FrictionBarTests
{
    private static readonly Vector2 Size = new(60, 180);

    /// <summary>A 1:1 field with a bar whose line is at x = 500, spanning y 400..600.</summary>
    private static (Entity Player, PlayfieldView View) Setup()
    {
        var store = new EntityStore();
        store.CreateEntity(new FrictionBar(new Vector2(500 - FrictionBar.Thickness / 2, 400), 200));
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(2000, 2000));
        return (store.CreateEntity(new Player(Size)), view);
    }

    [Test]
    public void PassingTheLineCrossesTheBar()
    {
        (Entity player, PlayfieldView view) = Setup();
        Assert.That(FrictionBarSystem.TryCross(player, view, 490, new Vector2(510, 510), out _), Is.True);
        Assert.That(FrictionBarSystem.TryCross(player, view, 510, new Vector2(490, 510), out _), Is.True);
    }

    [Test]
    public void StayingOnOneSideDoesNotCross()
    {
        (Entity player, PlayfieldView view) = Setup();
        Assert.That(FrictionBarSystem.TryCross(player, view, 480, new Vector2(495, 510), out _), Is.False);
    }

    [Test]
    public void PassingAboveTheBarDoesNotCross()
    {
        (Entity player, PlayfieldView view) = Setup();
        // The player's box spans 200..380, wholly above the bar's top at 400.
        Assert.That(FrictionBarSystem.TryCross(player, view, 490, new Vector2(510, 290), out _), Is.False);
    }

    [Test]
    public void FrictionIsOffUntilABarTurnsItOn()
    {
        var store = new EntityStore();
        Entity player = store.CreateEntity(new Player(Size));
        Assert.That(PlayerFriction.IsOn(player), Is.False);

        player.AddComponent(new PlayerFriction { On = true });
        Assert.That(PlayerFriction.IsOn(player), Is.True);

        PlayerFriction.Release(player);
        Assert.That(PlayerFriction.IsOn(player), Is.False);
    }
}
