using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Model.Ecs.Components;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Systems.Input;
using Xanadu.Screens.SongSelect;
using Position = Xanadu.Core.Position;

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
        return tile.GetComponent<Position>().Value;
    }

    private void MovePointer(FakePointerInput pointer, Vector2 position, bool pressed)
    {
        pointer.Current = new PointerInput(position, pressed);
        Frame();
    }

    [Test]
    public void ClickingAnEmptyRowSelectsNothing()
    {
        FakePointerInput pointer = OpenCarousel();
        Vector2 empty = Carousel().Tiles.First(t => t.GetComponent<SongSelectTile>().Index < 0)
            .GetComponent<Position>().Value;

        MovePointer(pointer, empty, true);
        MovePointer(pointer, empty, false);

        Assert.That(SelectedLevel(), Is.EqualTo(0));
    }
}
