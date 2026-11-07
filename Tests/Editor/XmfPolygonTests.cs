using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfPolygonTests
{
    private static PolygonDocument Sample()
    {
        var item = new Xif { Name = "rock" };
        int index = XifEditing.AddPolygon(item, new() { new(-40, -10), new(30, -25), new(45, 20), new(-20, 30) });
        item.Primitives[index].Fill = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        item.Primitives[index].Shader = XifShaderRef.OfCommon(CommonShaders.Paths.First());
        return new PolygonDocument
        {
            X = 300, Y = 420, Rotation = 35f, SelfModulate = 0x40a0ffccu, Groups = new() { 1, 4 }, Item = item,
        };
    }

    private static void AssertSample(PolygonDocument polygon)
    {
        Assert.That(polygon.X, Is.EqualTo(300f));
        Assert.That(polygon.Y, Is.EqualTo(420f));
        Assert.That(polygon.Rotation, Is.EqualTo(35f));
        Assert.That(polygon.SelfModulate, Is.EqualTo(0x40a0ffccu));
        Assert.That(polygon.Groups, Is.EqualTo(new[] { 1, 4 }));
        Assert.That(polygon.Item.Name, Is.EqualTo("rock"));
        XifPrimitive primitive = polygon.Item.Primitives.Single();
        Assert.That(primitive.IsPolygon, Is.True);
        Assert.That(primitive.Points, Is.EqualTo(new[] { new Vector2(-40, -10), new Vector2(30, -25), new Vector2(45, 20), new Vector2(-20, 30) }));
        Assert.That(primitive.Fill, Is.EqualTo(new Color(0.2f, 0.4f, 0.6f, 0.8f)));
        Assert.That(primitive.Shader.Path, Is.EqualTo(CommonShaders.Paths.First()));
    }

    [Test]
    public void PolygonsRoundTripThroughAMap()
    {
        var source = new LevelDocument { Name = "Shapes" };
        source.Polygons.Add(Sample());

        AssertSample(XmfBinary.Read(XmfBinary.Write(source)).Polygons.Single());
    }

    [Test]
    public void PolygonsRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        mapset.Variants.Add(new XmfVariant { Id = "easy", Name = "Easy" });
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Polygons.Add(Sample());
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Polygons, Is.Empty);
        AssertSample(read.Variants.Single(v => v.Id == "hard").Polygons.Single());
    }

    [Test]
    public void AMapWithoutPolygonsHasNoPolygonChunk()
    {
        byte[] data = XmfBinary.Write(new LevelDocument { Name = "Plain" });
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet((uint)XmfChunk.Polygons, out _), Is.False);
    }

    [Test]
    public void TextAndGrassKeepTheirTurnInMapsAndMapsets()
    {
        var source = new LevelDocument();
        source.Texts.Add(new TextDocument { Text = "turned", Rotation = 90f });
        source.Grass.Add(new GrassDocument { Rotation = 180f, Seed = 3 });
        LevelDocument map = XmfBinary.Read(XmfBinary.Write(source));
        Assert.That(map.Texts.Single().Rotation, Is.EqualTo(90f));
        Assert.That(map.Grass.Single().Rotation, Is.EqualTo(180f));

        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var a = new XmfVariant { Id = "a" };
        a.Texts.Add(new TextDocument { Text = "a", Rotation = 12f });
        var b = new XmfVariant { Id = "b" };
        b.Texts.Add(new TextDocument { Text = "b", Rotation = 34f });
        b.Grass.Add(new GrassDocument { Rotation = 56f });
        mapset.Variants.Add(a);
        mapset.Variants.Add(b);
        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));
        Assert.That(read.Variants[0].Texts.Single().Rotation, Is.EqualTo(12f));
        Assert.That(read.Variants[1].Texts.Single().Rotation, Is.EqualTo(34f));
        Assert.That(read.Variants[1].Grass.Single().Rotation, Is.EqualTo(56f));
    }

    [Test]
    public void TheSerializerDropsShapesWithNothingToDraw()
    {
        var level = new LevelDocument();
        level.Polygons.Add(Sample());
        var empty = new Xif();
        empty.Primitives.Add(new XifPrimitive { Kind = XifPrimitive.PolygonKind, Points = new() { Vector2.Zero, Vector2.One } });
        level.Polygons.Add(new PolygonDocument { Item = empty, Rotation = 400f });
        level.Polygons[0].Rotation = -90f;

        LevelDocument read = LevelSerializer.Normalize(XmfBinary.Read(XmfBinary.Write(level)));

        Assert.That(read.Polygons, Has.Count.EqualTo(1));
        Assert.That(read.Polygons[0].Rotation, Is.EqualTo(270f));
    }
}
