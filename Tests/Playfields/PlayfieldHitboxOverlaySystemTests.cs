using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayfieldHitboxOverlaySystemTests : EcsSystemTest
{
    private ScriptedToggle _toggle = null!;
    private RecordingCanvas _canvas = null!;

    protected override void OnEcsSetUp()
    {
        _toggle = new ScriptedToggle();
        _canvas = new RecordingCanvas();
        Root.Add(new PlayfieldHitboxOverlaySystem(
            Store,
            _canvas,
            default,
            _toggle));
    }

    [Test]
    public void HiddenUntilToggled()
    {
        AddPlayfield();
        Frame();

        Assert.That(_canvas.Visible, Is.False);
        Assert.That(_canvas.Rects, Is.Empty);
    }

    [Test]
    public void TheToggleDrawsThePlayerAndBlockBoxes()
    {
        AddPlayfield();
        Store.CreateEntity(new Block(new Vector2(100, 200), new Vector2(200, 100)));
        Store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 400)));

        _toggle.Pressed = true;
        Frame();

        Assert.That(_canvas.Visible, Is.True);
        Assert.That(
            _canvas.Rects.ConvertAll(rect => rect.Bounds),
            Does.Contain(new Rect2(150, 200, 100, 50)));
        Assert.That(
            _canvas.Rects.ConvertAll(rect => rect.Bounds),
            Does.Contain(new Rect2(385, 350, 30, 100)));
        Assert.That(
            _canvas.Rects.ConvertAll(rect => rect.Color),
            Does.Contain(PlayfieldRenderer.BlockHitboxColor));
        Assert.That(
            _canvas.Rects.ConvertAll(rect => rect.Color),
            Does.Contain(PlayfieldRenderer.PlayerHitboxColor));
    }

    [Test]
    public void TogglingAgainHidesAndClearsTheBoxes()
    {
        AddPlayfield();
        Store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 400)));

        _toggle.Pressed = true;
        Frame();
        Assert.That(_canvas.Visible, Is.True);

        _toggle.Pressed = false;
        Frame();
        _toggle.Pressed = true;
        Frame();

        Assert.That(_canvas.Visible, Is.False);
        Assert.That(_canvas.Rects, Is.Empty);
    }

    [Test]
    public void LeavingThePlayfieldClearsTheBoxes()
    {
        AddPlayfield();
        Store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 400)));

        _toggle.Pressed = true;
        Frame();
        Assert.That(_canvas.Rects, Is.Not.Empty);

        foreach (Entity entity in Store.Query<PlayfieldView>().ToEntityList())
            entity.DeleteEntity();
        Frame();

        Assert.That(_canvas.Rects, Is.Empty);
    }

    private void AddPlayfield() =>
        Store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new PlayfieldMode(PlayfieldKind.Play));

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;

        public bool IsTogglePressed() => Pressed;
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public bool Visible { get; private set; }

        public List<(Rect2 Bounds, Color Color)> Rects { get; } = new();

        public void SetVisible(Rid canvas, bool visible) => Visible = visible;

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) => Rects.Clear();

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
            Rects.Add((bounds, color));

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
