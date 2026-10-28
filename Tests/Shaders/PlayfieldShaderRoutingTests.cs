using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class PlayfieldShaderRoutingTests
{
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(1920, 1080));
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly Playhead Head = new(1000, 120, 0, true);

    [Test]
    public void EachNoteIsDrawnUnderItsOwnShaderAndTheSlotIsClearedAfterwards()
    {
        var store = new EntityStore();
        store.CreateEntity(new Note(1000, new Vector2(100, 100)), new ItemShader(2));
        store.CreateEntity(new Note(1000, new Vector2(300, 100)));
        store.CreateEntity(new Note(1000, new Vector2(500, 100)), new ItemShader(5));
        var canvas = new LogCanvas();

        PlayfieldRenderer.DrawNotes(canvas, default, store, View, Field, Head, default);

        // Each note selects its slot before any of its drawing; none leaks into the next.
        List<int> slotsAtDraw = canvas.SlotAtEachDraw;
        Assert.That(canvas.Selections, Does.Contain(2));
        Assert.That(canvas.Selections, Does.Contain(5));
        Assert.That(canvas.Selections[^1], Is.EqualTo(0), "the slot is reset once the notes are done");
        Assert.That(slotsAtDraw, Does.Contain(2));
        Assert.That(slotsAtDraw, Does.Contain(5));
        Assert.That(slotsAtDraw.IndexOf(0), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void ANoteWithTheCommonRainbowShaderIsDrawnUnderItAndItIsClearedAfterwards()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new Note(1000, new Vector2(100, 100)),
            new ItemCommonShader(CommonShaders.Paths[0]));
        store.CreateEntity(new Note(1000, new Vector2(300, 100)));
        var canvas = new LogCanvas();

        PlayfieldRenderer.DrawNotes(canvas, default, store, View, Field, Head, default);

        Assert.That(canvas.CommonSelections, Does.Contain(CommonShaders.Paths[0]));
        Assert.That(canvas.CommonAtEachDraw, Does.Contain(CommonShaders.Paths[0]));
        Assert.That(canvas.CommonSelections[^1], Is.Empty, "the common shader is cleared once the notes are done");
        Assert.That(canvas.Selections[^1], Is.EqualTo(0), "the slot is cleared too");
    }

    [Test]
    public void MinesAndBlocksAreShadedToo()
    {
        var store = new EntityStore();
        store.CreateEntity(new Mine(1000, new Vector2(100, 100), 600, 600), new ItemShader(3));
        store.CreateEntity(new Block(new Vector2(200, 200), new Vector2(50, 50)), new ItemShader(4));
        var canvas = new LogCanvas();

        PlayfieldRenderer.DrawMines(canvas, default, store, View, Head);
        PlayfieldRenderer.DrawBlocks(canvas, default, store, View);

        Assert.That(canvas.SlotAtEachDraw, Does.Contain(3));
        Assert.That(canvas.SlotAtEachDraw, Does.Contain(4));
        Assert.That(canvas.Selections[^1], Is.EqualTo(0));
    }

    private sealed class LogCanvas : IRectCanvas
    {
        private int _slot;
        private string _commonPath = "";

        public List<int> Selections { get; } = new();
        public List<int> SlotAtEachDraw { get; } = new();
        public List<string> CommonSelections { get; } = new();
        public List<string> CommonAtEachDraw { get; } = new();

        public void SelectShader(int slot)
        {
            _slot = slot;
            Selections.Add(slot);
        }

        public void SelectCommonShader(string path)
        {
            _commonPath = path;
            CommonSelections.Add(path);
        }

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Mark();

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) => Mark();

        public void DrawPolyline(Rid canvas, ReadOnlySpan<Vector2> points, Color color, float width, bool antialiased = true) =>
            Mark();

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) => Mark();

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) => Mark();

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) => Mark();

        private void Mark()
        {
            SlotAtEachDraw.Add(_slot);
            CommonAtEachDraw.Add(_commonPath);
        }
    }
}
