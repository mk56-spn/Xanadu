using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

/// <summary>Where the play camera aims: on the player, held inside a big field, still on a small one.</summary>
[TestFixture]
public sealed class PlayCameraTests
{
    private static readonly PlayCamera Camera = new(new Rect2(40, 90, 1840, 930));

    [Test]
    public void ABigFieldCentresThePlayerAwayFromTheEdges()
    {
        var view = new PlayfieldView(new Vector2(40, 90), 1f, new Vector2(1920, 1080));
        var field = new Playfield(5000, 5000);

        Vector2 target = PlayCameraSystem.Target(Camera, new Vector2(2540, 2590), view, field);

        var shown = new Rect2(Camera.Viewport.Position + target, Camera.Viewport.Size);
        Assert.That(shown.GetCenter().DistanceTo(new Vector2(2540, 2590)), Is.LessThan(0.01f));
    }

    [Test]
    public void TheViewStopsAtTheFieldsEdge()
    {
        var view = new PlayfieldView(new Vector2(40, 90), 1f, new Vector2(1920, 1080));
        var field = new Playfield(5000, 5000);

        Vector2 target = PlayCameraSystem.Target(Camera, new Vector2(60, 5080), view, field);

        Assert.That(target.X, Is.EqualTo(0f), "the left edge of the field is the left of the view");
        Assert.That(target.Y + Camera.Viewport.End.Y, Is.EqualTo(90f + 5000f).Within(0.01f), "the bottom too");
    }

    [Test]
    public void ASmallFieldStaysCentredWhereverThePlayerGoes()
    {
        var view = new PlayfieldView(new Vector2(500, 90), 0.93f, new Vector2(1920, 1080));
        var field = new Playfield(1000, 1000);

        Vector2 left = PlayCameraSystem.Target(Camera, new Vector2(510, 500), view, field);
        Vector2 right = PlayCameraSystem.Target(Camera, new Vector2(1400, 900), view, field);

        Assert.That(left, Is.EqualTo(right));
        Assert.That(left.Y, Is.EqualTo(0f).Within(0.01f), "a field exactly as tall as the view does not move");
    }
}
