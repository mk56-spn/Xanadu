using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Rendering;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfFlareTests
{
    private static FlareDocument Sample() => new()
    {
        X = 320.5f,
        Y = 140f,
        Width = 600f,
        Height = 200f,
        Rotation = 30f,
        Axes = 2,
        Thickness = 0.08f,
        Falloff = 2.25f,
        Core = 0.2f,
        Halo = 0.6f,
        Glow = 0.45f,
        Intensity = 1.7f,
        Heat = 0.4f,
        Color = 0xff9a3cffu,
        SelfModulate = 0x40a0ffccu,
        Groups = new() { 1, 4 },
        Layer = 3,
        Z = 2,
        Blend = BlendMode.Sub,
        ShaderSource = 4,
    };

    private static void AssertSample(FlareDocument flare)
    {
        Assert.That(flare.X, Is.EqualTo(320.5f));
        Assert.That(flare.Width, Is.EqualTo(600f));
        Assert.That(flare.Height, Is.EqualTo(200f));
        Assert.That(flare.Rotation, Is.EqualTo(30f));
        Assert.That(flare.Axes, Is.EqualTo(2));
        Assert.That(flare.Falloff, Is.EqualTo(2.25f));
        Assert.That(flare.Intensity, Is.EqualTo(1.7f));
        Assert.That(flare.Heat, Is.EqualTo(0.4f));
        Assert.That(flare.Color, Is.EqualTo(0xff9a3cffu));
        Assert.That(flare.SelfModulate, Is.EqualTo(0x40a0ffccu));
        Assert.That(flare.Groups, Is.EqualTo(new[] { 1, 4 }));
        Assert.That((flare.Layer, flare.Z), Is.EqualTo((3, 2)));
        Assert.That(flare.Blend, Is.EqualTo(BlendMode.Sub));
        Assert.That(flare.ShaderSource, Is.EqualTo(4));
    }

    [Test]
    public void FlaresRoundTripThroughAMap()
    {
        var source = new LevelDocument { Name = "Flares" };
        source.Flares.Add(Sample());

        AssertSample(XmfBinary.Read(XmfBinary.Write(source)).Flares.Single());
    }

    [Test]
    public void AMapWithoutFlaresReadsWithNone()
    {
        Assert.That(XmfBinary.Read(XmfBinary.Write(new LevelDocument { Name = "Dark" })).Flares, Is.Empty);
    }

    [Test]
    public void FlaresRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        mapset.Variants.Add(new XmfVariant { Id = "easy", Name = "Easy" });
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Flares.Add(Sample());
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Flares, Is.Empty);
        AssertSample(read.Variants.Single(v => v.Id == "hard").Flares.Single());
    }

    [Test]
    public void SettingsOutOfRangeAreBroughtBackWhenLoaded()
    {
        LevelFlare flare = LevelDocumentMapper.ToFlare(new FlareDocument
        {
            Width = -5f,
            Height = 9_999f,
            Axes = 7,
            Thickness = float.NaN,
            Intensity = 50f,
            Heat = -1f,
        });

        Assert.That(flare.Width, Is.EqualTo(LevelFlare.MinSize));
        Assert.That(flare.Height, Is.EqualTo(LevelFlare.MaxSize));
        Assert.That(flare.Axes, Is.EqualTo(2));
        Assert.That(flare.Thickness, Is.EqualTo(LevelFlare.MinThickness));
        Assert.That(flare.Intensity, Is.EqualTo(LevelFlare.MaxIntensity));
        Assert.That(flare.Heat, Is.Zero);
    }

    [Test]
    public void ANewFlareAddsItsLight()
    {
        var store = new Friflo.Engine.ECS.EntityStore();
        Assert.That(ItemBlend.Of(EditorItems.Create(store, new LevelFlare())), Is.EqualTo(BlendMode.Add));
    }
}
