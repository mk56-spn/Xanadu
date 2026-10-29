using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Scenes;

/// <summary>
/// Song timing has no scene-test case (it needs a catalog song), so this covers
/// the migrated builder directly: the live handles the controller writes to, the
/// adjust buttons, and the action row's layout.
/// </summary>
[TestFixture]
public sealed class SongTimingSceneTests
{
    private EntityStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        UiBuilder.DefaultVisuals = new FakeVisualRidFactory();
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    private SongDocument Build()
    {
        var song = new SongDocument
        {
            Id = "test",
            Title = "Night Drive",
            Bpm = 128,
            OffsetMs = -42,
        };

        Entity root = _store.CreateEntity();
        SongTimingScene.Build(_store, root, song);

        var systems = new SystemRoot(_store);
        systems.Add(new ContainerLayoutSystem());
        systems.Update(new UpdateTick(1f / 60f, 1f));
        systems.Update(new UpdateTick(1f / 60f, 2f));

        return song;
    }

    [Test]
    public void TheActionRowOffersAddSong()
    {
        Build();

        var actions = _store.Query<SceneCommand>().ToEntityList()
            .Select(entity => entity.GetComponent<SceneCommand>().Action)
            .ToList();

        Assert.That(actions, Does.Contain(SceneActionId.AddSong));
        Assert.That(actions, Does.Contain(SceneActionId.Back));
    }

    [Test]
    public void TheViewPointsAtTheThreeLiveLabels()
    {
        Build();

        Entity view = _store.Query<SongTimingView>().ToEntityList()[0];
        SongTimingView timing = view.GetComponent<SongTimingView>();

        Assert.That(timing.Title.GetComponent<Text>().Value, Is.EqualTo("Night Drive"));
        Assert.That(timing.Bpm.GetComponent<Text>().Value, Is.EqualTo("BPM 128"));
        Assert.That(timing.Offset.GetComponent<Text>().Value, Is.EqualTo("OFFSET -42 MS"));
    }

    [Test]
    public void EveryAdjustAndActionIsWired()
    {
        Build();

        Assert.That(_store.Query<SongTimingAdjust>().Count, Is.EqualTo(4));
        Assert.That(_store.Query<SongTimingSave>().Count, Is.EqualTo(1));

        bool hasBack = _store.Query<SceneCommand>().ToEntityList()
            .Any(entity => entity.GetComponent<SceneCommand>().Action == SceneActionId.Back);
        Assert.That(hasBack, Is.True);
    }

    [Test]
    public void TheActionRowIsCenteredAndTheAdjustButtonsFlankTheirValues()
    {
        Build();

        Rect2 save = Area(ByText("SAVE"));
        Rect2 back = Area(ByText("BACK"));
        Rect2 minus = Area(ByText("-1 BPM"));
        Rect2 plus = Area(ByText("+1 BPM"));
        Rect2 bpm = Area(ByText("BPM 128"));

        Assert.That(save.End.X, Is.LessThan(back.Position.X));
        Assert.That((save.Position.X + back.End.X) / 2,
            Is.EqualTo(UiStage.Full.Center.X).Within(0.5f));

        // The value box is deliberately wider than its text, so the adjust
        // buttons flank its centre rather than clearing its edges.
        Assert.That(CenterX(bpm), Is.EqualTo(UiStage.Full.Center.X).Within(0.5f));
        Assert.That(CenterX(minus),
            Is.EqualTo(UiStage.Full.Center.X - 180).Within(0.5f));
        Assert.That(CenterX(plus),
            Is.EqualTo(UiStage.Full.Center.X + 180).Within(0.5f));
        Assert.That(minus.Position.X, Is.LessThan(bpm.Position.X));
    }

    private static float CenterX(Rect2 area) => area.Position.X + area.Size.X / 2;

    private Entity ByText(string text) =>
        _store.Query<Text>().ToEntityList()
            .First(entity => entity.GetComponent<Text>().Value == text);

    private static Rect2 Area(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 center = UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value);
        return new Rect2(center + bounds.Position, bounds.Size);
    }
}
