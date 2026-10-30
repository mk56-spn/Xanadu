using System.IO;
using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfMapsetBinaryTests
{
    [Test]
    public void RoundTripsAMapsetWithSharedShadersAndMultipleVariants()
    {
        var mapset = new XmfMapset
        {
            Id = "mapset-1",
            Slug = "song-mapset",
            SongId = "song-9",
        };
        mapset.Shaders.Add(new ShaderDocument
        {
            Slot = 1,
            Name = "Glow",
            Source = "shader_type canvas_item;\nvoid fragment() {}",
        });

        var easy = new XmfVariant { Id = "easy", Name = "Easy", PlayerShader = 1 };
        easy.Notes.Add(new NoteDocument { TimeMs = 100, X = 1, Y = 2 });
        mapset.Variants.Add(easy);

        var hard = new XmfVariant
        {
            Id = "hard",
            Name = "Hard",
            FieldShader = 1,
            BackgroundColor = "#20406080",
        };
        hard.Notes.Add(new NoteDocument
        {
            TimeMs = 50,
            X = 3,
            Y = 4,
            DashStrength = DashStrength.Double,
            PairIndex = PairIndex.Three,
            Momentum = 750f,
            ContactLeft = 1,
            ContactRight = 2,
            ChannelSide = ChannelSide.Right,
            HoldMs = 750,
        });
        hard.Notes.Add(new NoteDocument { TimeMs = 150, X = 5, Y = 6 });
        hard.Blocks.Add(new BlockDocument
        {
            X = 10,
            Y = 20,
            Width = 100,
            Height = 30,
            ShapeId = 9,
            ShapePiece = 1,
        });
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Id, Is.EqualTo("mapset-1"));
        Assert.That(read.SongId, Is.EqualTo("song-9"));
        Assert.That(read.Shaders.Single().Name, Is.EqualTo("Glow"));
        Assert.That(read.Variants, Has.Count.EqualTo(2));

        XmfVariant readEasy = read.Variants.Single(v => v.Id == "easy");
        Assert.That(readEasy.Name, Is.EqualTo("Easy"));
        Assert.That(readEasy.PlayerShader, Is.EqualTo(1));
        Assert.That(readEasy.BackgroundColor, Is.Empty);
        Assert.That(readEasy.Notes.Single().TimeMs, Is.EqualTo(100));
        Assert.That(readEasy.Notes.Single().DashStrength, Is.EqualTo(DashStrength.Single));

        XmfVariant readHard = read.Variants.Single(v => v.Id == "hard");
        Assert.That(readHard.FieldShader, Is.EqualTo(1));
        Assert.That(readHard.BackgroundColor, Is.EqualTo("#20406080"));
        Assert.That(readHard.Notes, Has.Count.EqualTo(2));
        Assert.That(readHard.Notes[0].DashStrength, Is.EqualTo(DashStrength.Double));
        Assert.That(readHard.Notes[0].PairIndex, Is.EqualTo(PairIndex.Three));
        Assert.That(readHard.Notes[0].Momentum, Is.EqualTo(750f));
        Assert.That(readHard.Notes[0].ContactLeft, Is.EqualTo(1));
        Assert.That(readHard.Notes[0].ContactRight, Is.EqualTo(2));
        Assert.That(readHard.Notes[0].ChannelSide,
            Is.EqualTo(ChannelSide.Right));
        Assert.That(readHard.Notes[0].HoldMs, Is.EqualTo(750));
        Assert.That(readHard.Notes[1].DashStrength, Is.EqualTo(DashStrength.Single));
        Assert.That(readHard.Blocks, Has.Count.EqualTo(1));
        Assert.That(readHard.Blocks[0].ShapeId, Is.EqualTo(9));
        Assert.That(readHard.Blocks[0].ShapePiece, Is.EqualTo(1));
    }

    [Test]
    public void MapsetVariantsWrittenBeforeBackgroundsExistedStillLoad()
    {
        var mapset = new XmfMapset
        {
            Id = "mapset-old",
            Slug = "old-mapset",
            SongId = "song-1",
        };
        mapset.Variants.Add(new XmfVariant
        {
            Id = "old",
            Name = "Old",
            BackgroundColor = "#204060",
        });

        byte[] current = XmfBinary.WriteMapset(mapset);
        ChunkReader reader = ChunkReader.Open(
            current,
            "XMF2"u8.ToArray(),
            XmfBinary.MapsetMajorVersion,
            "XMF2");
        var file = new ChunkWriter(
            "XMF2"u8.ToArray(),
            XmfBinary.MapsetMajorVersion,
            0);
        for (uint id = 1; id <= 4; id++)
        {
            if (!reader.TryGet(id, out Chunk chunk))
                continue;

            byte[] payload = chunk.ToArray();
            if (id == 4)
                payload = TrimVariantBackgrounds(payload);
            file.Add(id, payload);
        }

        XmfMapset old = XmfBinary.ReadMapset(file.ToArray());

        Assert.That(old.Variants.Single().BackgroundColor, Is.Empty);
    }

    /// <summary>Rebuilds a variants chunk as it was before each blob carried a background index.</summary>
    private static byte[] TrimVariantBackgrounds(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(input);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        uint count = reader.ReadUInt32();
        writer.Write(count);
        for (uint i = 0; i < count; i++)
        {
            uint length = reader.ReadUInt32();
            byte[] blob = reader.ReadBytes((int)length);
            writer.Write(length - sizeof(uint));
            writer.Write(blob, 0, blob.Length - sizeof(uint));
        }

        writer.Flush();
        return output.ToArray();
    }

    [Test]
    public void NormalizingAMapsetValidatesEveryVariantsShaderReferences()
    {
        var mapset = new XmfMapset();
        mapset.Shaders.Add(new ShaderDocument
        {
            Slot = 1,
            Source = "shader_type canvas_item;\nvoid fragment() {}",
        });

        var variant = new XmfVariant { PlayerShader = 1, FieldShader = 99 };
        variant.Notes.Add(new NoteDocument { Item = Xif.ForShader(1) });
        variant.Notes.Add(new NoteDocument { Item = Xif.ForShader(99) });
        mapset.Variants.Add(variant);

        XmfMapset clean = LevelSerializer.NormalizeMapset(mapset);

        XmfVariant cleanVariant = clean.Variants.Single();
        Assert.That(cleanVariant.PlayerShader, Is.EqualTo(1));
        Assert.That(cleanVariant.FieldShader, Is.EqualTo(0));
        Assert.That(cleanVariant.Notes[0].Item.PrimaryShader, Is.EqualTo(1));
        Assert.That(cleanVariant.Notes[1].Item.Shaders, Is.Empty);
    }
}
