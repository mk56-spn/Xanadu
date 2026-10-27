using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Physics;
using Writer.Systems.Playfields;
using Writer.Tests.Support;

namespace Writer.Tests.Groups;

[TestFixture]
public sealed class GroupModulationSystemTests : EcsSystemTest
{
    private Entity _level;
    private LevelGroups _groups;

    protected override void OnEcsSetUp()
    {
        Root.Add(new GroupModulationSystem(Store));
        _groups = LevelGroups.Create();
        _level = Store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new PlayfieldMode(PlayfieldKind.Play),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            _groups);
    }

    private void At(long ms) => _level.GetComponent<Playhead>().TimeMs = ms;

    private static Entity Member(EntityStore store, IComponent item, params int[] groups)
    {
        Entity entity = store.CreateEntity();
        switch (item)
        {
            case Note note: entity.AddComponent(note); break;
            case Mine mine: entity.AddComponent(mine); break;
            case Block block: entity.AddComponent(block); break;
            case StartPoint start: entity.AddComponent(start); break;
        }

        entity.AddComponent(new ItemGroups(ItemGroups.FromList(groups)));
        return entity;
    }

    [Test]
    public void ANoteInAGroupIsMovedByItsMoveAtTheirTime()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0.4f, 0.6f, new Vector2(100, 0)));
        Entity note = Member(Store, new Note(5000, new Vector2(300, 400)), 0);

        At(300);
        Frame();
        Assert.That(note.GetComponent<Note>().Position, Is.EqualTo(new Vector2(300, 400)));

        At(500);
        Frame();
        Assert.That(note.GetComponent<Note>().Position.X, Is.EqualTo(350f).Within(0.01f));

        At(900);
        Frame();
        Assert.That(note.GetComponent<Note>().Position, Is.EqualTo(new Vector2(400, 400)));
    }

    [Test]
    public void SeekingBackPutsAnItemBackWhereItStarted()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0)));
        Entity note = Member(Store, new Note(5000, new Vector2(300, 400)), 0);

        At(2000);
        Frame();
        Assert.That(note.GetComponent<Note>().Position.X, Is.EqualTo(400f).Within(0.01f));

        At(0);
        Frame();
        Assert.That(note.GetComponent<Note>().Position, Is.EqualTo(new Vector2(300, 400)));
    }

    [Test]
    public void AnItemInTwoGroupsMovesBySumOfBoth()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0)));
        _groups.Groups[2].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(0, -30)));
        Entity both = Member(Store, new Note(5000, new Vector2(300, 400)), 0, 2);
        Entity one = Member(Store, new Note(5000, new Vector2(300, 400)), 0);

        At(1000);
        Frame();

        Assert.That(both.GetComponent<Note>().Position, Is.EqualTo(new Vector2(400, 370)));
        Assert.That(one.GetComponent<Note>().Position, Is.EqualTo(new Vector2(400, 400)));
    }

    [Test]
    public void MinesAndBlocksMoveToo()
    {
        _groups.Groups[1].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(10, 20)));
        Entity mine = Member(Store, new Mine(5000, new Vector2(100, 100)), 1);
        Entity block = Member(Store, new Block(new Vector2(200, 300), new Vector2(50, 50)), 1);

        At(1000);
        Frame();

        Assert.That(mine.GetComponent<Mine>().Position, Is.EqualTo(new Vector2(110, 120)));
        Assert.That(block.GetComponent<Block>().Position, Is.EqualTo(new Vector2(210, 320)));
        Assert.That(block.GetComponent<Block>().Size, Is.EqualTo(new Vector2(50, 50)));
    }

    [Test]
    public void StartPointsMoveWithTheirGroupsToo()
    {
        _groups.Groups[1].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(10, 20)));
        Entity start = Member(Store, new StartPoint(
            "Intro",
            5000,
            new Vector2(100, 100),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)), 1);

        At(1000);
        Frame();

        Assert.That(start.GetComponent<StartPoint>().Position, Is.EqualTo(new Vector2(110, 120)));
    }

    [Test]
    public void ItemsOutsideEveryGroupAreLeftAlone()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0)));
        Entity loose = Store.CreateEntity(new Note(5000, new Vector2(300, 400)));
        Entity unrelated = Member(Store, new Note(5000, new Vector2(1, 2)), 4);

        At(1000);
        Frame();

        Assert.That(loose.GetComponent<Note>().Position, Is.EqualTo(new Vector2(300, 400)));
        Assert.That(unrelated.GetComponent<Note>().Position, Is.EqualTo(new Vector2(1, 2)));
    }

    [Test]
    public void TheEditorDoesNotMoveItsItems()
    {
        _level.GetComponent<PlayfieldMode>().Kind = PlayfieldKind.Editor;
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0)));
        Entity note = Member(Store, new Note(5000, new Vector2(300, 400)), 0);

        At(1000);
        Frame();

        Assert.That(note.GetComponent<Note>().Position, Is.EqualTo(new Vector2(300, 400)));
        Assert.That(note.HasComponent<GroupColor>(), Is.False);
    }

    [Test]
    public void AGroupColourIsPublishedForDrawing()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Colour(1f, 2f, Colors.Red));
        Entity note = Member(Store, new Note(5000, new Vector2(300, 400)), 0);

        At(500);
        Frame();
        Assert.That(note.GetComponent<GroupColor>().Coverage, Is.EqualTo(0f));

        At(1500);
        Frame();
        Assert.That(note.GetComponent<GroupColor>().Coverage, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(GroupColor.For(note, Colors.Blue).R, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void ALaterColourWinsOverAnEarlierOneForAnItemInBothGroups()
    {
        _groups.Groups[0].Effects.Add(GroupEffect.Colour(1f, 1.1f, Colors.Red));
        _groups.Groups[1].Effects.Add(GroupEffect.Colour(2f, 2.1f, Colors.Green));
        Entity note = Member(Store, new Note(5000, new Vector2(0, 0)), 0, 1);

        At(5000);
        Frame();

        Color drawn = GroupColor.For(note, Colors.Black);
        Assert.That(drawn.G, Is.EqualTo(1f).Within(0.01f));
        Assert.That(drawn.R, Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void AMovedBlocksCollisionBoxFollowsIt()
    {
        using var physics = new Box2DPhysicsWorld();
        Root.Add(new BlockBodySystem(physics, Store));
        _groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(200, 0)));
        Entity block = Member(Store, new Block(new Vector2(100, 100), new Vector2(100, 100)), 0);

        Frame();
        Assert.That(block.HasComponent<PhysicsBody>(), Is.True);
        float before = Box2DPhysicsWorld.ToScreen(block.GetComponent<PhysicsBody>().Body.GetPosition()).X;

        At(1000);
        Frame(2);

        float after = Box2DPhysicsWorld.ToScreen(block.GetComponent<PhysicsBody>().Body.GetPosition()).X;
        Assert.That(after - before, Is.EqualTo(200f).Within(0.5f));
    }
}
