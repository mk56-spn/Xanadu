using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Xanadu.Systems.Canvas;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorGridDrawTests : AnimatorToolTest
{
    private LineCanvas _canvas = null!;

    protected override void AddSystems()
    {
        _canvas = new LineCanvas();
        Root.Add(new AnimatorGridDrawSystem(_canvas, default));
    }

    [Test]
    public void GridAxesCrossAtTheSkeletonOrigin()
    {
        Rect2 area = AnimatorGeometry.PanelArea(Ui.Preview);
        Vector2 origin = AnimatorGeometry.PreviewOrigin(area);

        List<LineCall> axes = _canvas.Lines.FindAll(line => line.Color == CanvasGrid.AxisColor);
        Assert.That(axes, Has.Count.EqualTo(2));
        Assert.That(axes.Exists(line => line.From.X == origin.X && line.To.X == origin.X), Is.True);
        Assert.That(axes.Exists(line => line.From.Y == origin.Y && line.To.Y == origin.Y), Is.True);
        Assert.That(_canvas.Lines.TrueForAll(line => area.Grow(0.5f).HasPoint(line.From) &&
                                                     area.Grow(0.5f).HasPoint(line.To)), Is.True);
    }

    [Test]
    public void GridIsOnlyRedrawnWhenThePreviewMoves()
    {
        int clears = _canvas.Clears;
        Frame(3);

        Assert.That(clears, Is.EqualTo(1));
        Assert.That(_canvas.Clears, Is.EqualTo(1));
    }

    private sealed class LineCanvas : IRectCanvas
    {
        public List<LineCall> Lines { get; } = new();
        public int Clears { get; private set; }

        public void Clear(Rid canvas)
        {
            Clears++;
            Lines.Clear();
        }

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
            Lines.Add(new LineCall(from, to, color));

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }

    private readonly record struct LineCall(Vector2 From, Vector2 To, Color Color);
}
