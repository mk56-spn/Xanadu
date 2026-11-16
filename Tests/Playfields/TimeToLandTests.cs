using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Model.Ecs.Components;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

/// <summary>Looking ahead along her fall for the ground she will land on, so she can brace for it.</summary>
[TestFixture]
public sealed class TimeToLandTests
{
    private static readonly Vector2 Size = new(60, 180);

    /// <summary>A 1:1 field 1000 tall, so her feet meet the floor at y 1000.</summary>
    private static (Entity Player, PlayfieldView View, Playfield Field) Setup(EntityStore store)
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        return (store.CreateEntity(new Player(Size)), view, new Playfield(1000, 1000));
    }

    [Test]
    public void AFallFindsTheFloorAhead()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup(new EntityStore());
        // Feet at 900, falling at 1000 px/s with no gravity: 0.1 s to go.
        float? left = PlayerGround.TimeToLand(
            player,
            view,
            field,
            new Vector2(500, 810),
            new Vector2(0, 1000),
            0f,
            0.15f);

        Assert.That(left, Is.EqualTo(0.1f).Within(1f / 120f));
    }

    [Test]
    public void GroundTooFarOffIsNotFound()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup(new EntityStore());
        Assert.That(
            PlayerGround.TimeToLand(player, view, field, new Vector2(500, 410), new Vector2(0, 1000), 0f, 0.15f),
            Is.Null);
    }

    [Test]
    public void ABlockTopUnderHerPathCounts()
    {
        var store = new EntityStore();
        store.CreateEntity(new Block(new Vector2(400, 700), new Vector2(200, 40)));
        (Entity player, PlayfieldView view, Playfield field) = Setup(store);

        float? left = PlayerGround.TimeToLand(
            player,
            view,
            field,
            new Vector2(500, 560),
            new Vector2(0, 500),
            0f,
            0.15f);

        Assert.That(left, Is.EqualTo(0.1f).Within(1f / 120f));
    }

    [Test]
    public void RisingFindsNothing()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup(new EntityStore());
        Assert.That(
            PlayerGround.TimeToLand(player, view, field, new Vector2(500, 800), new Vector2(0, -1000), 0f, 0.15f),
            Is.Null);
    }
}
