using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Items;

namespace Xanadu.Tests.Editor;

/// <summary>Everything with a shape turns to any angle; only blocks keep to quarter turns.</summary>
[TestFixture]
public sealed class FreeRotationTests
{
    private readonly EntityStore _store = new();
    private static readonly Playfield Field = new(1000, 1000);

    private static void Turn(Entity entity, Vector2 pivot, float degrees) =>
        SelectionRotation.Apply(SelectionRotation.Capture(entity), pivot, Mathf.DegToRad(degrees), Field, snap: false);

    [Test]
    public void TextTurnsOnTheSpotToAnyAngle()
    {
        Entity text = EditorItems.Create(_store, new LevelText(new Vector2(300, 300), "HI"));
        Assert.That(SelectionRotation.TryGetBounds(new[] { text }, out _), Is.True, "one text has a handle");

        Turn(text, new Vector2(300, 300), 37f);

        Assert.That(text.GetComponent<LevelText>().RotationDegrees, Is.EqualTo(37f).Within(0.01f));
        Assert.That(text.GetComponent<LevelText>().Position, Is.EqualTo(new Vector2(300, 300)));
    }

    [Test]
    public void GrassTurnsAboutTheMiddleOfItsBox()
    {
        Entity grass = EditorItems.Create(_store, new GrassPatch(new Vector2(500, 500), 1) { Width = 200, Length = 60 });
        Vector2 middle = grass.GetComponent<GrassPatch>().Bounds.GetCenter();

        Turn(grass, middle, 90f);

        GrassPatch turned = grass.GetComponent<GrassPatch>();
        Assert.That(turned.RotationDegrees, Is.EqualTo(90f).Within(0.01f));
        Assert.That(turned.Bounds.GetCenter().DistanceTo(middle), Is.LessThan(0.01f));
    }

    [Test]
    public void APolygonOrbitsThePivotAndTurns()
    {
        var item = new Xif();
        XifEditing.AddPolygon(item);
        Entity shape = EditorItems.Create(_store, new LevelPolygon(new Vector2(600, 500), item));

        Turn(shape, new Vector2(500, 500), 90f);

        LevelPolygon polygon = shape.GetComponent<LevelPolygon>();
        Assert.That(polygon.Position.DistanceTo(new Vector2(500, 600)), Is.LessThan(0.01f));
        Assert.That(polygon.RotationDegrees, Is.EqualTo(90f).Within(0.01f));
    }

    [Test]
    public void FlippingAPolygonMirrorsItsOutlineExactly()
    {
        var item = new Xif();
        XifEditing.AddPolygon(item, new() { new(0, 0), new(40, 0), new(0, 20) });
        Entity shape = EditorItems.Create(_store, new LevelPolygon(new Vector2(500, 500), item, 30f));
        Vector2[] before = shape.GetComponent<LevelPolygon>().Outline(item.Primitives[0]);

        SelectionRotation.ApplyFlip(SelectionRotation.Capture(shape), new Vector2(500, 500), horizontal: true, Field, snap: false);

        LevelPolygon flipped = shape.GetComponent<LevelPolygon>();
        Vector2[] after = flipped.Outline(flipped.Item.Primitives[0]);
        for (int i = 0; i < before.Length; i++)
            Assert.That(after[i].DistanceTo(new Vector2(1000 - before[i].X, before[i].Y)), Is.LessThan(0.01f));
    }

    [Test]
    public void GrassResizesWithTheBoxHandles()
    {
        Entity grass = EditorItems.Create(_store, new GrassPatch(new Vector2(500, 500), 1) { Width = 200, Length = 60 });

        Assert.That(GridPlacement.TryGet(grass, out Rect2 box, out _), Is.True);
        Rect2 wider = GridPlacement.Resize(box, GridHandle.Right, new Vector2(700, 470), Field, snap: false);
        GridPlacement.SetBounds(grass, wider);
        Rect2 taller = GridPlacement.Resize(wider, GridHandle.Top, new Vector2(550, 400), Field, snap: false);
        GridPlacement.SetBounds(grass, taller);

        GrassPatch patch = grass.GetComponent<GrassPatch>();
        Assert.That(patch.Width, Is.EqualTo(300f));
        Assert.That(patch.Length, Is.EqualTo(100f));
        Assert.That(patch.Position, Is.EqualTo(new Vector2(550, 500)), "the blades still stand on the same ground");
    }

    [Test]
    public void TurnedBoxesPickInsideTheTurnOnly()
    {
        var box = new Rect2(0, 0, 100, 20);
        Assert.That(TurnedBox.Contains(box, 0f, new Vector2(90, 10)), Is.True);
        Assert.That(TurnedBox.Contains(box, 90f, new Vector2(90, 10)), Is.False);
        Assert.That(TurnedBox.Contains(box, 90f, new Vector2(50, 50)), Is.True);
    }
}
