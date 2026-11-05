using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfGrassTests
{
    private static GrassDocument Sample() => new()
    {
        X = 120.5f,
        Y = 340f,
        Width = 512f,
        Length = 72f,
        Density = 55f,
        Sway = 20f,
        Color = 0x2f8f3aff,
        Variation = 0.45f,
        Seed = 0xdeadbeef,
        SelfModulate = 0x40a0ffccu,
        Groups = new() { 2, 5 },
    };

    [Test]
    public void GrassRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Grass" };
        source.Grass.Add(Sample());

        GrassDocument grass = XmfBinary.Read(XmfBinary.Write(source)).Grass.Single();

        Assert.That(grass.X, Is.EqualTo(120.5f));
        Assert.That(grass.Y, Is.EqualTo(340f));
        Assert.That(grass.Width, Is.EqualTo(512f));
        Assert.That(grass.Length, Is.EqualTo(72f));
        Assert.That(grass.Density, Is.EqualTo(55f));
        Assert.That(grass.Sway, Is.EqualTo(20f));
        Assert.That(grass.Color, Is.EqualTo(0x2f8f3affu));
        Assert.That(grass.Variation, Is.EqualTo(0.45f));
        Assert.That(grass.Seed, Is.EqualTo(0xdeadbeefu));
        Assert.That(grass.SelfModulate, Is.EqualTo(0x40a0ffccu));
        Assert.That(grass.Groups, Is.EqualTo(new[] { 2, 5 }));
    }

    [Test]
    public void AMapWithoutGrassHasNoGrassChunk()
    {
        byte[] data = XmfBinary.Write(new LevelDocument { Name = "Plain" });
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet(15, out _), Is.False);
        Assert.That(XmfBinary.Read(data).Grass, Is.Empty);
    }

    [Test]
    public void GrassRoundTripsThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        mapset.Variants.Add(new XmfVariant { Id = "easy", Name = "Easy" });
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Grass.Add(Sample());
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Grass, Is.Empty);
        GrassDocument grass = read.Variants.Single(v => v.Id == "hard").Grass.Single();
        Assert.That(grass.Width, Is.EqualTo(512f));
        Assert.That(grass.Seed, Is.EqualTo(0xdeadbeefu));
    }

    [Test]
    public void SettingsOutOfRangeAreBroughtBackWhenLoaded()
    {
        GrassPatch patch = LevelDocumentMapper.ToPatch(new GrassDocument
        {
            Width = -5f,
            Length = 9_999f,
            Density = float.NaN,
            Sway = 400f,
            Variation = 3f,
        });

        Assert.That(patch.Width, Is.EqualTo(GrassPatch.MinWidth));
        Assert.That(patch.Length, Is.EqualTo(GrassPatch.MaxLength));
        Assert.That(patch.Density, Is.EqualTo(GrassPatch.MinDensity));
        Assert.That(patch.Sway, Is.EqualTo(GrassPatch.MaxSway));
        Assert.That(patch.Variation, Is.EqualTo(1f));
    }
}
