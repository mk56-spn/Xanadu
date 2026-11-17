using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Kinds;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Model.Settings;

namespace Xanadu.Tests.Playfields;

/// <summary>Curve notes: their timing comes from length and speed, and they keep through files and undo.</summary>
[TestFixture]
public sealed class CurveNoteTests
{
    [Test]
    public void AStraightCurveAtOneSpeedTakesItsLengthOverItsSpeed()
    {
        CurvePath path = CurvePath.Of(1000, [new Vector2(0, 0), new Vector2(600, 0)], [600f, 600f]);

        Assert.That(path.Length, Is.EqualTo(600f).Within(0.01f));
        Assert.That(path.EndMs, Is.EqualTo(2000));
        (Vector2 middle, Vector2 velocity) = path.At(1500);
        Assert.That(middle.X, Is.EqualTo(300f).Within(0.5f));
        Assert.That(velocity.X, Is.EqualTo(600f).Within(0.5f));
    }

    [Test]
    public void SpeedEasesAlongTheLengthBetweenPoints()
    {
        // Speed rising evenly with length from 600 to 1200 over 900 units takes 900 / 600 · ln 2 seconds.
        CurvePath path = CurvePath.Of(0, [new Vector2(0, 0), new Vector2(900, 0)], [600f, 1200f]);

        Assert.That(path.EndMs, Is.EqualTo(1040).Within(2));
        Assert.That(path.At(path.EndMs).Velocity.X, Is.EqualTo(1200f).Within(1f));
    }

    [Test]
    public void ThePathRunsThroughEveryPoint()
    {
        Vector2[] points = [new(0, 0), new(300, -200), new(600, 0)];
        CurvePath path = CurvePath.Of(0, points, [600f, 600f, 600f]);

        foreach (Vector2 point in points)
            Assert.That(path.DistanceTo(point), Is.LessThan(0.01f));
        Assert.That(path.At(path.TimeAtAnchor(1)).Position.Y, Is.EqualTo(-200f).Within(1f));
    }

    [Test]
    public void ACurveNoteRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Curves" };
        source.Notes.Add(new NoteDocument
        {
            TimeMs = 1234,
            X = 10,
            Y = 20,
            Target = KeybindAction.Action2,
            Path =
            [
                new CurvePointDocument { X = 10, Y = 20, Speed = 600 },
                new CurvePointDocument { X = 200, Y = 50, Speed = 900 },
                new CurvePointDocument { X = 400, Y = 20, Speed = 1200 },
            ],
        });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        NoteDocument note = read.Notes.Single();
        Assert.That(note.TimeMs, Is.EqualTo(1234));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.Action2));
        Assert.That(note.Path!.Select(p => p.Speed), Is.EqualTo(new[] { 600f, 900f, 1200f }));
        Assert.That(note.Path![1].X, Is.EqualTo(200f));
    }

    [Test]
    public void ANoteWithoutAPathStillReadsAsAPlainNote()
    {
        var source = new LevelDocument { Name = "Plain" };
        source.Notes.Add(new NoteDocument { TimeMs = 500, X = 1, Y = 2 });

        NoteDocument note = XmfBinary.Read(XmfBinary.Write(source)).Notes.Single();

        Assert.That(note.Path, Is.Null);
        Assert.That(note.BuildPath(), Is.Null);
    }

    [Test]
    public void ACurveNoteRoundTripsThroughAMapsetVariant()
    {
        var level = new LevelDocument { Name = "Set" };
        level.Notes.Add(new NoteDocument
        {
            Path =
            [
                new CurvePointDocument { X = 0, Y = 0, Speed = 600 },
                new CurvePointDocument { X = 100, Y = 0, Speed = 600 },
            ],
        });
        var mapset = new XmfMapset();
        mapset.Variants.Add(XmfVariant.From(level));

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single().Notes.Single().Path, Has.Count.EqualTo(2));
    }

    [Test]
    public void LoadingKeepsTheNoteButDropsAPathWithOnlyItsHead()
    {
        var level = new LevelDocument();
        level.Notes.Add(new NoteDocument { X = 1, Y = 1, Path = [new CurvePointDocument { X = 1, Y = 1, Speed = 600 }] });

        LevelDocument read = LevelSerializer.Deserialize(XmfBinary.Write(level));

        Assert.That(read.Notes, Has.Count.EqualTo(1));
        Assert.That(read.Notes[0].Path, Is.Null);
    }

    [Test]
    public void ReshapingACurveNoteLeavesItsCopyAlone()
    {
        var store = new EntityStore();
        var path = new NotePath([new Vector2(0, 0), new Vector2(100, 0)], [600f, 600f]);
        Entity curve = EditorItems.Create(store, new Note(0, Vector2.Zero, path: path));
        ItemCopy copy = ItemKinds.Notes.Copy(curve);

        ref Note note = ref curve.GetComponent<Note>();
        note.Path = note.Path!.WithPoint(1, new Vector2(500, 500));

        Assert.That(((Note)copy.Value).Path!.Points[1], Is.EqualTo(new Vector2(100, 0)));
        Assert.That(ItemKinds.Of(curve), Is.SameAs(ItemKinds.Notes));
        Assert.That(ItemKinds.Notes.Same(copy.Value, curve.GetComponent<Note>()), Is.False);
    }

    [Test]
    public void MovingACurveNoteMovesItsHeadAndEveryPoint()
    {
        var store = new EntityStore();
        var path = new NotePath([new Vector2(10, 10), new Vector2(100, 10)], [600f, 600f]);
        Entity curve = EditorItems.Create(store, new Note(0, new Vector2(10, 10), path: path));

        ItemKinds.Notes.SetPosition(curve, new Vector2(20, 30));

        Note moved = curve.GetComponent<Note>();
        Assert.That(moved.Position, Is.EqualTo(new Vector2(20, 30)));
        Assert.That(moved.Path!.Points.ToArray(), Is.EqualTo(new[] { new Vector2(20, 30), new Vector2(110, 30) }));
    }
}
