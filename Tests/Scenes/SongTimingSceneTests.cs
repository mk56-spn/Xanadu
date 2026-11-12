using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Scenes;

/// <summary>
/// Song timing has no scene-test case (it needs a catalog song), so this covers
/// the builder directly: the live handles the controller writes to, the step
/// buttons around their values, the action row's layout, and the changes list.
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

        var actions = _store.Query<UiCommand>().ToEntityList()
            .Select(entity => entity.GetComponent<UiCommand>().As<SceneActionId>())
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

        Assert.That(UiCommand.All<SongTimingAdjustKind>(_store), Has.Count.EqualTo(4));
        Assert.That(UiCommand.Find(_store, SongTempoAction.Save).IsNull, Is.False);

        bool hasBack = _store.Query<UiCommand>().ToEntityList()
            .Any(entity => entity.GetComponent<UiCommand>().As<SceneActionId>() == SceneActionId.Back);
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

        // The value box is deliberately wider than its text, so the step buttons
        // beside it stay put however long the value gets.
        Assert.That(CenterX(bpm), Is.EqualTo(UiStage.Full.Center.X).Within(0.5f));
        Assert.That(minus.End.X, Is.LessThan(bpm.Position.X));
        Assert.That(plus.Position.X, Is.GreaterThan(bpm.End.X));
        Assert.That(CenterX(minus) + CenterX(plus), Is.EqualTo(2 * UiStage.Full.Center.X).Within(0.5f));
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

    [Test]
    public void TheScreenHasTypedTimingAndABpmChangesList()
    {
        Build();

        SongTimingView timing = _store.Query<SongTimingView>().ToEntityList()[0].GetComponent<SongTimingView>();
        // Pooled rows past the last change are hidden, so their fields are disabled entities.
        var kinds = _store.Query<SongTimingField>().WithDisabled().ToEntityList()
            .Select(entity => entity.GetComponent<SongTimingField>().Kind)
            .ToList();

        // The changes are a pooled list: one set of fields per pooled row.
        int pool = UiLists.Pool(timing.List);
        Assert.That(timing.List.HasComponent<UiList>(), Is.True);
        Assert.That(timing.Rows, Has.Length.EqualTo(pool));
        Assert.That(kinds, Does.Contain(SongTimingFieldKind.BaseBpm));
        Assert.That(kinds, Does.Contain(SongTimingFieldKind.Offset));
        Assert.That(kinds.Count(kind => kind == SongTimingFieldKind.ChangeBpm), Is.EqualTo(pool));
        Assert.That(
            UiCommand.All<SongTempoAction>(_store).Select(button => button.Id),
            Does.Contain(SongTempoAction.Add));
    }
}
