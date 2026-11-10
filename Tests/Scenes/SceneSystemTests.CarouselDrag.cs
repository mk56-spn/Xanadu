using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Scenes;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Scenes;

public sealed partial class SceneSystemTests
{
    private const float DragRowPitch = 60;

    /// <summary>Opens song select on 20 levels with the real input system feeding the drag.</summary>
    private FakePointerInput OpenCarousel()
    {
        AddLevels(20);
        var pointer = new FakePointerInput();
        _root.Add(new InputSystem(pointer));
        _root.Add(new SongSelectDragSystem(pointer, _scenes));
        _root.Add(new SongSelectScrollSystem());
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        return pointer;
    }

    private SongSelectState Carousel() =>
        _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>();

    /// <summary>The middle of the row showing a level, as the pointer would find it.</summary>
    private Vector2 RowCenter(int level)
    {
        Entity tile = Carousel().Tiles.First(t => t.GetComponent<SongSelectTile>().Index == level);
        return tile.GetComponent<Xanadu.Ecs.Position>().Value;
    }

    private void MovePointer(FakePointerInput pointer, Vector2 position, bool pressed)
    {
        pointer.Current = new PointerInput(position, pressed);
        Frame();
    }

    [Test]
    public void DraggingARowUpMovesTheListWithThePointerAndTheSelectionFollows()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 start = RowCenter(0);

        MovePointer(pointer, start, true);
        MovePointer(pointer, start + new Vector2(0, -10), true);
        Vector2 held = start + new Vector2(0, -10 - DragRowPitch * 2.25f);
        MovePointer(pointer, held, true);
        Assert.That(Carousel().Scroll, Is.GreaterThan(0f).And.LessThan(2.25f), "the list eases after the pointer");

        for (int i = 0; i < 60; i++)
            MovePointer(pointer, held, true);

        Assert.That(Carousel().Scroll, Is.EqualTo(2.25f).Within(0.001f));
        Assert.That(SelectedLevel(), Is.EqualTo(2));
    }

    [Test]
    public void TheListHoldsItsPlaceWhileThePointerRestsMidDrag()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 start = RowCenter(0);
        MovePointer(pointer, start, true);
        MovePointer(pointer, start + new Vector2(0, -10), true);
        Vector2 held = start + new Vector2(0, -10 - DragRowPitch * 1.4f);

        for (int i = 0; i < 60; i++)
            MovePointer(pointer, held, true);

        Assert.That(Carousel().Scroll, Is.EqualTo(1.4f).Within(0.001f));
    }

    [Test]
    public void ReleasingADragEasesToTheNearestLevelWithoutClickingTheGrabbedRow()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 start = RowCenter(1);
        MovePointer(pointer, start, true);
        MovePointer(pointer, start + new Vector2(0, -10), true);
        Vector2 end = start + new Vector2(0, -10 - DragRowPitch * 3.25f);
        MovePointer(pointer, end, true);

        MovePointer(pointer, end, false);
        for (int i = 0; i < 120; i++)
            Frame();

        Assert.That(SelectedLevel(), Is.EqualTo(3));
        Assert.That(Carousel().Scroll, Is.EqualTo(3f));
    }

    [TestCase(-5000f, 19)]
    [TestCase(5000f, 0)]
    public void ADragStopsAtBothEndsOfTheList(float distance, int selected)
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 start = RowCenter(0);
        MovePointer(pointer, start, true);
        MovePointer(pointer, start + new Vector2(0, distance), true);
        for (int i = 0; i < 120; i++)
            MovePointer(pointer, start + new Vector2(0, distance * 2), true);

        Assert.That(SelectedLevel(), Is.EqualTo(selected));
        Assert.That(Carousel().Scroll, Is.EqualTo(selected));
    }

    [Test]
    public void AClickThatWobblesStillSelectsTheRow()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 start = RowCenter(2);

        MovePointer(pointer, start, true);
        Assert.That(SelectedLevel(), Is.EqualTo(0), "a press alone must not jump the list");
        MovePointer(pointer, start + new Vector2(30, 5), true);
        MovePointer(pointer, start + new Vector2(30, 5), false);

        Assert.That(SelectedLevel(), Is.EqualTo(2));
    }

    [Test]
    public void ClickingAnEmptyRowSelectsNothing()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 empty = Carousel().Tiles
            .First(t => t.GetComponent<SongSelectTile>().Index < 0)
            .GetComponent<Xanadu.Ecs.Position>().Value;

        MovePointer(pointer, empty, true);
        MovePointer(pointer, empty, false);

        Assert.That(SelectedLevel(), Is.EqualTo(0));
    }
}
