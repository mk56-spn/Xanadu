using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

/// <summary>
/// The XMF record layout: every item kind carries the same header, a record
/// with fields a reader doesn't know still reads, and a cut-short record is
/// refused rather than read wrong.
/// </summary>
[TestFixture]
public sealed class XmfRecordTests
{
    private static T Headed<T>(T item) where T : ItemDocument
    {
        item.Groups = new List<int> { 1, 9 };
        item.Shader = 3;
        item.ShaderSource = 9;
        item.Layer = -4;
        item.Z = 6;
        item.Blend = BlendMode.Mul;
        item.SelfModulate = 0x11223344u;
        return item;
    }

    private static void AssertHeader(ItemDocument item, string kind)
    {
        Assert.That(item.Groups, Is.EqualTo(new[] { 1, 9 }), kind);
        Assert.That(item.Shader, Is.EqualTo(3), kind);
        Assert.That(item.ShaderSource, Is.EqualTo(9), kind);
        Assert.That((item.Layer, item.Z), Is.EqualTo((-4, 6)), kind);
        Assert.That(item.Blend, Is.EqualTo(BlendMode.Mul), kind);
        Assert.That(item.SelfModulate, Is.EqualTo(0x11223344u), kind);
    }

    [Test]
    public void EveryItemKindKeepsTheSharedHeader()
    {
        var source = new LevelDocument { Name = "Header" };
        source.Notes.Add(Headed(new NoteDocument()));
        source.Mines.Add(Headed(new MineDocument()));
        source.StartPoints.Add(Headed(new StartPointDocument { Name = "s" }));
        source.Blocks.Add(Headed(new BlockDocument { Width = 10, Height = 10 }));
        source.Spikes.Add(Headed(new SpikeDocument()));
        source.Texts.Add(Headed(new TextDocument { Text = "t" }));
        source.Grass.Add(Headed(new GrassDocument()));
        source.Flares.Add(Headed(new FlareDocument()));
        source.Polygons.Add(Headed(new PolygonDocument()));
        source.ScreenReads.Add(Headed(new ScreenReadDocument()));

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        AssertHeader(read.Notes.Single(), "note");
        AssertHeader(read.Mines.Single(), "mine");
        AssertHeader(read.StartPoints.Single(), "start point");
        AssertHeader(read.Blocks.Single(), "block");
        AssertHeader(read.Spikes.Single(), "spike");
        AssertHeader(read.Texts.Single(), "text");
        AssertHeader(read.Grass.Single(), "grass");
        AssertHeader(read.Flares.Single(), "flare");
        AssertHeader(read.Polygons.Single(), "polygon");
        AssertHeader(read.ScreenReads.Single(), "screen read");
    }

    [Test]
    public void AnEmptyMapHasOnlyItsStringsAndMeta()
    {
        byte[] data = XmfBinary.Write(new LevelDocument { Name = "Empty" });
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        foreach (XmfChunk chunk in System.Enum.GetValues<XmfChunk>())
        {
            bool expected = chunk is XmfChunk.Strings or XmfChunk.Meta;
            Assert.That(reader.TryGet((uint)chunk, out _), Is.EqualTo(expected), chunk.ToString());
        }
    }

    [Test]
    public void FieldsAReaderDoesNotKnowAreSkipped()
    {
        var source = new LevelDocument { Name = "Newer" };
        source.Mines.Add(new MineDocument { TimeMs = 100, X = 1 });
        source.Mines.Add(new MineDocument { TimeMs = 200, X = 2 });

        // As a newer writer would: every mine record gains a field at its end.
        byte[] data = RewriteChunk(XmfBinary.Write(source), XmfChunk.Mines, record => [.. record, 1, 2, 3, 4]);

        List<MineDocument> mines = XmfBinary.Read(data).Mines;
        Assert.That(mines.Select(m => (m.TimeMs, m.X)), Is.EqualTo(new[] { (100L, 1f), (200L, 2f) }));
    }

    [Test]
    public void ACutShortRecordIsRefused()
    {
        var source = new LevelDocument { Name = "Short" };
        source.Mines.Add(new MineDocument());

        byte[] data = RewriteChunk(XmfBinary.Write(source), XmfChunk.Mines, record => record[..^4]);

        Assert.Throws<XmfFormatException>(() => XmfBinary.Read(data));
    }

    [Test]
    public void AMapsetVariantKeepsEveryKindItsLevelWould()
    {
        var level = new LevelDocument { Name = "V" };
        level.ScreenReads.Add(new ScreenReadDocument { Width = 321 });
        level.Flares.Add(new FlareDocument { Axes = 2 });
        var mapset = new XmfMapset { SongId = "song" };
        mapset.Variants.Add(XmfVariant.From(level));

        XmfVariant read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset)).Variants.Single();

        Assert.That(read.ScreenReads.Single().Width, Is.EqualTo(321f));
        Assert.That(read.Flares.Single().Axes, Is.EqualTo(2));
    }

    /// <summary>The map with each record of one list chunk passed through <paramref name="change"/>.</summary>
    private static byte[] RewriteChunk(byte[] map, XmfChunk target, System.Func<byte[], byte[]> change)
    {
        ChunkReader reader = ChunkReader.Open(map, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");
        var file = new ChunkWriter("XMF1"u8.ToArray(), XmfBinary.MajorVersion, XmfBinary.MinorVersion);
        foreach (XmfChunk id in System.Enum.GetValues<XmfChunk>())
        {
            if (!reader.TryGet((uint)id, out Chunk chunk))
                continue;
            file.Add((uint)id, id == target ? RewriteRecords(chunk.ToArray(), change) : chunk.ToArray());
        }

        return file.ToArray();
    }

    private static byte[] RewriteRecords(byte[] list, System.Func<byte[], byte[]> change)
    {
        using var input = new BinaryReader(new MemoryStream(list));
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        uint count = input.ReadUInt32();
        writer.Write(count);
        for (uint i = 0; i < count; i++)
        {
            byte[] record = change(input.ReadBytes((int)input.ReadUInt32()));
            writer.Write((uint)record.Length);
            writer.Write(record);
        }

        writer.Flush();
        return output.ToArray();
    }
}
