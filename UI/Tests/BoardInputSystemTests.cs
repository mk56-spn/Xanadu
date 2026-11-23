using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

[TestFixture]
public sealed class BoardInputSystemTests : EcsSystemTest
{
    private ScriptedPointer _pointer = null!;
    private FakeKeyboard _keyboard = null!;
    private FakeWheel _wheel = null!;
    private FakeEntry _entry = null!;
    private ITextEntry _hostEntry = null!;
    private Board _board = null!;
    private Entity _entity;
    private int _changes;

    protected override void OnEcsSetUp()
    {
        _pointer = new ScriptedPointer();
        _keyboard = new FakeKeyboard();
        _wheel = new FakeWheel();
        _entry = new FakeEntry();
        _hostEntry = HostPlatform.TextEntry;
        HostPlatform.TextEntry = _entry;
        Root.Add(new BoardInputSystem(_pointer, _keyboard, _wheel));
        Root.Add(new BoardDrawSystem(HostPlatform.NewCanvas()));

        _board = new Board();
        _entity = Store.CreateEntity(
            new Rectangle(new Rect2(0, 0, 800, 600)),
            new Position(Vector2.Zero),
            new UiBoard(_board));
        _entity.OnBoardChanged(_ => _changes++);
        _changes = 0;
    }

    [TearDown]
    public void RestoreEntry() => HostPlatform.TextEntry = _hostEntry;

    private void Press(Vector2 at, bool shift = false)
    {
        _pointer.Current = new PointerInput(at, true, shift);
        Frame();
    }

    private void Release(Vector2 at)
    {
        _pointer.Current = new PointerInput(at, false);
        Frame();
    }

    private void Click(Vector2 at)
    {
        Press(at);
        Release(at);
    }

    private void Tap(Key key)
    {
        _keyboard.Down.Add(key);
        Frame();
        _keyboard.Down.Remove(key);
        Frame();
    }

    private void Drag(Vector2 from, Vector2 to)
    {
        Press(from);
        _pointer.Current = new PointerInput((from + to) / 2, true);
        Frame();
        _pointer.Current = new PointerInput(to, true);
        Frame();
        Release(to);
    }

    [Test]
    public void DoubleClickingEmptyBoardMakesABoxToTypeInto()
    {
        Click(new Vector2(300, 200));
        Click(new Vector2(300, 200));
        _entry.Typed = "Playfield";
        Frame();
        Tap(Key.Enter);

        Assert.That(_board.Boxes, Has.Count.EqualTo(1));
        Assert.That(_board.Boxes[0].Text, Is.EqualTo("Playfield"));
        Assert.That(_board.Boxes[0].Rect.HasPoint(new Vector2(300, 200)), Is.True);
        Assert.That(_entity.GetComponent<UiBoard>().State.Editing, Is.Zero);
        Assert.That(_changes, Is.EqualTo(1));
        Assert.That(_entry.Claimed, Is.False);
    }

    [Test]
    public void ANewBoxLeftEmptyIsTakenBackOff()
    {
        Click(new Vector2(300, 200));
        Click(new Vector2(300, 200));
        Tap(Key.Escape);

        Assert.That(_board.Boxes, Is.Empty);
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void DraggingAFrameCarriesWhatItHolds()
    {
        BoardBox frame = _board.Add(new Rect2(100, 100, 400, 300), "Game");
        BoardBox inner = _board.Add(new Rect2(150, 150, 100, 50), "Audio");
        Frame();

        Drag(new Vector2(400, 350), new Vector2(450, 380));

        Assert.That(frame.Rect.Position, Is.EqualTo(new Vector2(150, 130)));
        Assert.That(inner.Rect.Position, Is.EqualTo(new Vector2(200, 180)));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public void DraggingTheDotOntoAnotherBoxLinksThem()
    {
        BoardBox from = _board.Add(new Rect2(100, 100, 100, 60));
        BoardBox to = _board.Add(new Rect2(400, 100, 100, 60));
        Frame();

        Drag(BoardInputSystem.HandleOf(from.Rect), new Vector2(450, 130));

        Assert.That(_board.Links, Is.EqualTo(new[] { new BoardLink(from.Id, to.Id) }));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public void DeleteTakesTheBoxAndItsLinesAndUndoBringsThemBack()
    {
        BoardBox a = _board.Add(new Rect2(100, 100, 100, 60));
        BoardBox b = _board.Add(new Rect2(400, 100, 100, 60));
        _board.Link(a.Id, b.Id);
        Frame();

        Click(new Vector2(120, 120));
        Tap(Key.Delete);
        Assert.That(_board.Boxes, Has.Count.EqualTo(1));
        Assert.That(_board.Links, Is.Empty);

        _keyboard.Down.Add(Key.Ctrl);
        Tap(Key.Z);
        Assert.That(_board.Boxes, Has.Count.EqualTo(2));
        Assert.That(_board.Links, Has.Count.EqualTo(1));

        Tap(Key.Y);
        Assert.That(_board.Boxes, Has.Count.EqualTo(1));
    }

    [Test]
    public void ADigitColoursTheSelection()
    {
        BoardBox box = _board.Add(new Rect2(100, 100, 100, 60));
        Frame();

        Click(new Vector2(120, 120));
        Tap(Key.Key3);

        Assert.That(box.Category, Is.EqualTo(2));
    }

    [Test]
    public void DraggingACornerResizesSnapped()
    {
        BoardBox box = _board.Add(new Rect2(100, 100, 100, 60));
        Frame();

        Drag(new Vector2(198, 158), new Vector2(263, 204));

        Assert.That(box.Rect.Size, Is.EqualTo(new Vector2(160, 100)));
    }

    [Test]
    public void TheWheelZoomsAboutThePointer()
    {
        Frame();
        BoardState state = _entity.GetComponent<UiBoard>().State;
        Vector2 pointer = new(400, 300);
        Vector2 before = state.ToBoard(pointer);
        _pointer.Current = new PointerInput(pointer, false);
        _wheel.Pending = 2;
        Frame();

        Assert.That(state.Zoom, Is.GreaterThan(1));
        Assert.That(Vector2.Distance(state.ToBoard(pointer), before), Is.LessThan(0.01f));
    }

    [Test]
    public void DraggingEmptyBoardPans()
    {
        Frame();
        Drag(new Vector2(400, 300), new Vector2(300, 250));

        Assert.That(_entity.GetComponent<UiBoard>().State.Pan, Is.EqualTo(new Vector2(100, 50)));
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void TheSmallestBoxUnderAPointWins()
    {
        BoardBox frame = _board.Add(new Rect2(0, 0, 500, 500));
        BoardBox inner = _board.Add(new Rect2(100, 100, 50, 50));

        Assert.That(_board.At(new Vector2(120, 120)), Is.SameAs(inner));
        Assert.That(_board.At(new Vector2(300, 300)), Is.SameAs(frame));
        Assert.That(_board.IsFrame(frame), Is.True);
        Assert.That(_board.IsFrame(inner), Is.False);
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        public readonly HashSet<Key> Down = new();

        public bool IsPressed(Key key) => Down.Contains(key);
    }

    private sealed class FakeWheel : IWheelSource
    {
        public int Pending;

        public int Notches => Pending;

        public bool Control => false;

        public bool Shift => false;

        public int TakeWheel()
        {
            int notches = Pending;
            Pending = 0;
            return notches;
        }
    }

    private sealed class FakeEntry : ITextEntry
    {
        public string Typed = "";

        public bool Claimed { get; set; }

        public string TakeTyped()
        {
            string typed = Typed;
            Typed = "";
            return typed;
        }

        public string Clipboard { get; set; } = "";
    }
}
