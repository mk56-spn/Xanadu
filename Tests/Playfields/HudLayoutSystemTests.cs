using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Systems.Playfields;
using Writer.Tests.Support;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class HudLayoutSystemTests : EcsSystemTest
{
    private RecordingCanvas _canvas = null!;
    private ScriptedPointer _pointer = null!;
    private FakeKeyboard _keyboard = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingCanvas();
        _pointer = new ScriptedPointer();
        _keyboard = new FakeKeyboard();
        Root.Add(new HudLayoutSystem(
            _canvas,
            _pointer,
            _keyboard,
            new HudLayoutSettings(persist: false)));
    }

    [Test]
    public void DragMovesAndSavesTheWholeItem()
    {
        Entity member = Store.CreateEntity(
            new VisualRid(default),
            new Position(new Vector2(100, 100)));
        Entity playfield = Store.CreateEntity(
            new HudLayout(),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(800, 600)));
        ref HudLayout layout = ref playfield.GetComponent<HudLayout>();
        layout.Add(
            "score",
            new Vector2(100, 100),
            new Rect2(80, 80, 40, 40),
            member);

        _keyboard.Press(Key.F7);
        _pointer.Current = new PointerInput(new Vector2(100, 100), true);
        Frame();
        _pointer.Current = new PointerInput(new Vector2(140, 120), true);
        Frame();

        Assert.That(member.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(140, 120)));
    }

    [Test]
    public void RightClickTogglesItemVisibility()
    {
        Entity member = Store.CreateEntity(
            new VisualRid(default),
            new Position(new Vector2(100, 100)));
        Entity playfield = Store.CreateEntity(
            new HudLayout(),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(800, 600)));
        ref HudLayout layout = ref playfield.GetComponent<HudLayout>();
        HudItemLayout item = layout.Add(
            "score",
            new Vector2(100, 100),
            new Rect2(80, 80, 40, 40),
            member);

        _keyboard.Press(Key.F7);
        _pointer.Current = new PointerInput(
            new Vector2(100, 100), true, SecondaryPressed: true);
        Frame();

        Assert.That(item.Visible, Is.False);

        _keyboard.Release(Key.F7);
        _pointer.Current = new PointerInput(Vector2.Zero, false);
        Frame();
        _keyboard.Press(Key.F7);
        Frame();

        Assert.That(layout.Editing, Is.False);
        Assert.That(item.Visible, Is.False);
        Assert.That(_canvas.Hidden, Does.Contain(member.GetComponent<VisualRid>().Value));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public HashSet<Rid> Hidden { get; } = new();

        public void SetVisible(Rid canvas, bool visible)
        {
            if (!visible)
                Hidden.Add(canvas);
        }

        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
