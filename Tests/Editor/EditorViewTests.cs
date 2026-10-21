using Godot;
using NUnit.Framework;
using Writer.Ecs;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorViewTests
{
    [Test]
    public void PlayfieldCoordinatesRoundTripThroughScreenSpace()
    {
        var view = new EditorView(
            new Vector2(30, 40),
            scale: 0.6f,
            new Vector2(1152, 648));

        Vector2 screen = view.PlayfieldToScreen(new Vector2(500, 500));
        Vector2 back = view.ScreenToPlayfield(screen);

        Assert.That(screen, Is.EqualTo(new Vector2(330, 340)));
        Assert.That(back.X, Is.EqualTo(500).Within(0.001f));
        Assert.That(back.Y, Is.EqualTo(500).Within(0.001f));
    }

    [Test]
    public void PlayfieldCornersMapToScreen()
    {
        var view = new EditorView(
            new Vector2(30, 30),
            scale: 0.5f,
            new Vector2(1152, 648));

        Assert.That(view.PlayfieldToScreen(Vector2.Zero), Is.EqualTo(new Vector2(30, 30)));
        Assert.That(
            view.PlayfieldToScreen(new Vector2(1000, 1000)),
            Is.EqualTo(new Vector2(530, 530)));
    }
}
