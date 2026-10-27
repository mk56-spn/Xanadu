using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class UiFocusSystemTests
{
    private EntityStore _store = null!;
    private IVisualRidFactory _visuals = null!;
    private UiBuilder _ui = null!;
    private FakeKeyboard _keyboard = null!;
    private FakePointer _pointer = null!;
    private SystemRoot _root = null!;
    private Entity _first;
    private Entity _second;
    private UiFocusSystem _focus = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _visuals = new FakeVisualRidFactory();
        _ui = UiBuilder.Begin(_store, _visuals).Layer(10);
        _keyboard = new FakeKeyboard();
        _pointer = new FakePointer();

        _first = Button("First", new Vector2(100, 100));
        _second = Button("Second", new Vector2(100, 200));

        _root = new SystemRoot(_store);
        _root.Add(new InputSystem(_pointer));
        _focus = new UiFocusSystem(
            _store,
            _pointer,
            _keyboard,
            _visuals,
            new Vector2(800, 600));
        _root.Add(_focus);
        _root.Add(new ToggleSystem());
    }

    [Test]
    public void TabMovesFocusInReadingOrderAndShiftTabGoesBack()
    {
        Press(Key.Tab);
        Frame();
        Assert.That(_first.Tags.Has<Focused>(), Is.True);

        Press(Key.Tab);
        Frame();
        Assert.That(_second.Tags.Has<Focused>(), Is.True);
        Assert.That(_first.Tags.Has<Focused>(), Is.False);

        _keyboard.Press(Key.Shift);
        Press(Key.Tab);
        Frame();
        Assert.That(_first.Tags.Has<Focused>(), Is.True);
    }

    [Test]
    public void DisabledButtonsAreSkippedByTabAndInput()
    {
        _first.AddTag<Disabled>();
        _pointer.Input = new PointerInput(new Vector2(100, 100), true);
        Frame();
        Assert.That(_first.Tags.Has<Pressed>(), Is.False);

        _pointer.Input = new PointerInput(new Vector2(100, 100), false);
        Frame();
        Press(Key.Tab);
        Frame();

        Assert.That(_second.Tags.Has<Focused>(), Is.True);
        Assert.That(_first.Tags.Has<Focused>(), Is.False);
    }

    [Test]
    public void FocusLeavesADisabledControl()
    {
        Press(Key.Tab);
        Frame();
        Assert.That(_first.Tags.Has<Focused>(), Is.True);

        _first.AddTag<Disabled>();
        Frame();
        Assert.That(_first.Tags.Has<Focused>(), Is.False);

        Press(Key.Tab);
        Frame();
        Assert.That(_second.Tags.Has<Focused>(), Is.True);
    }

    [Test]
    public void EnterPressesTheFocusedButtonForOneFrame()
    {
        Press(Key.Tab);
        Frame();
        Assert.That(_first.Tags.Has<Focused>(), Is.True);

        Press(Key.Enter);
        Assert.That(_store.GetEntityById(_first.Id).Tags.Has<Pressed>(), Is.True);

        Frame();
        Assert.That(_store.GetEntityById(_first.Id).Tags.Has<Pressed>(), Is.False);
    }

    [Test]
    public void EnterActivatesTheFocusedToggle()
    {
        _first.AddComponent(new Toggle("On", "Off", false));

        Press(Key.Tab);
        Frame();
        Press(Key.Enter);

        Assert.That(_first.GetComponent<Toggle>().Value, Is.True);
    }

    [Test]
    public void PointerPressFocusesTheControlUnderIt()
    {
        _pointer.Input = new PointerInput(new Vector2(100, 200), true);
        Frame();
        Frame();

        Assert.That(_second.Tags.Has<Focused>(), Is.True);
        Assert.That(_first.Tags.Has<Focused>(), Is.False);
    }

    [Test]
    public void TheFocusRingSurroundsTheFocusedControl()
    {
        Press(Key.Tab);
        Frame();

        var edges = _store.Query<UiFocusRingEdge>().ToEntityList();
        Assert.That(edges.Count, Is.EqualTo(4));
        foreach (Entity edge in edges)
        {
            Assert.That(edge.GetComponent<Position>().Value,
                Is.Not.EqualTo(UiLayout.Hidden));
        }

        Entity top = edges[0];
        Assert.That(top.GetComponent<Position>().Value.X, Is.EqualTo(100f).Within(0.1f));
        Assert.That(top.GetComponent<Position>().Value.Y, Is.EqualTo(83f).Within(0.1f));
        Assert.That(top.GetComponent<Rectangle>().Bounds.Size.X,
            Is.EqualTo(84f).Within(0.1f));
    }

    private Entity Button(string label, Vector2 position)
    {
        Entity button = _ui.RectEntity(
            position,
            new Vector2(80, 30),
            Colors.Blue,
            label,
            takesInput: true);
        button.AddComponent(ButtonVisual.Create(Colors.Blue));
        return button;
    }

    private void Press(Key key)
    {
        _keyboard.Press(key);
        Frame();
        _keyboard.Release(key);
    }

    private void Frame() =>
        _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class FakeKeyboard : IKeyboardState
    {
        private readonly HashSet<Key> _down = new();

        public bool IsPressed(Key key) => _down.Contains(key);
        public void Press(Key key) => _down.Add(key);
        public void Release(Key key) => _down.Remove(key);
    }

    private sealed class FakePointer : IPointerInputSource
    {
        public PointerInput Input = new(Vector2.Zero, false);

        public PointerInput Read() => Input;
    }
}
