using System.Linq;
using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfTextTests
{
    private static TextDocument Sample() => new()
    {
        X = 120.5f,
        Y = 340f,
        Size = 96f,
        Text = "Hold on — ☆ here",
        SelfModulate = 0x40a0ffccu,
        Shader = 0,
        Groups = new() { 2, 5 },
    };

    [Test]
    public void TextRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Texts" };
        source.Texts.Add(Sample());
        source.Texts.Add(new TextDocument { X = 1, Y = 2, Text = "" });

        LevelDocument map = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(map.Texts, Has.Count.EqualTo(2));
        TextDocument text = map.Texts[0];
        Assert.That(text.X, Is.EqualTo(120.5f));
        Assert.That(text.Y, Is.EqualTo(340f));
        Assert.That(text.Size, Is.EqualTo(96f));
        Assert.That(text.Text, Is.EqualTo("Hold on — ☆ here"));
        Assert.That(text.SelfModulate, Is.EqualTo(0x40a0ffccu));
        Assert.That(text.Groups, Is.EqualTo(new[] { 2, 5 }));
        Assert.That(map.Texts[1].Text, Is.Empty);
    }

    [Test]
    public void AMapWithoutTextHasNoTextChunk()
    {
        byte[] data = XmfBinary.Write(new LevelDocument { Name = "Plain" });
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet((uint)XmfChunk.Texts, out _), Is.False);
        Assert.That(XmfBinary.Read(data).Texts, Is.Empty);
    }

    [Test]
    public void TextRoundTripsThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        mapset.Variants.Add(new XmfVariant { Id = "easy", Name = "Easy" });
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Texts.Add(Sample());
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Texts, Is.Empty);
        TextDocument text = read.Variants.Single(v => v.Id == "hard").Texts.Single();
        Assert.That(text.Text, Is.EqualTo("Hold on — ☆ here"));
        Assert.That(text.Size, Is.EqualTo(96f));
        Assert.That(text.Groups, Is.EqualTo(new[] { 2, 5 }));
    }

    [Test]
    public void OverlongTextIsCutToTheLimit()
    {
        var source = new LevelDocument();
        source.Texts.Add(new TextDocument { Text = new string('x', XmfBinary.MaxTextLength + 50) });

        LevelDocument map = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(map.Texts.Single().Text, Has.Length.EqualTo(XmfBinary.MaxTextLength));
    }
}
