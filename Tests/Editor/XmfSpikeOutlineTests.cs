using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfSpikeOutlineTests
{
    private static LevelDocument MapWithSpikes() => new()
    {
        Name = "Spikes",
        Spikes =
        {
            new SpikeDocument { X = 10, Y = 20, Width = 50, Height = 50 },
            new SpikeDocument { X = 90, Y = 20, Width = 50, Height = 50, OutlineWidth = 7f, OutlineColor = 0x11223344 },
        },
    };

    [Test]
    public void ALevelKeepsEachSpikesOutline()
    {
        LevelDocument map = XmfBinary.Read(XmfBinary.Write(MapWithSpikes()));

        Assert.That(map.Spikes[0].OutlineWidth, Is.EqualTo(Block.DefaultOutlineWidth));
        Assert.That(map.Spikes[0].OutlineColor, Is.EqualTo(Block.DefaultOutlineColor.ToRgba32()));
        Assert.That(map.Spikes[1].OutlineWidth, Is.EqualTo(7f));
        Assert.That(map.Spikes[1].OutlineColor, Is.EqualTo(0x11223344u));
    }

    [Test]
    public void AMapsetVariantKeepsEachSpikesOutline()
    {
        var mapset = new XmfMapset { Id = "set", Slug = "set", SongId = "song" };
        LevelDocument level = MapWithSpikes();
        mapset.Variants.Add(new XmfVariant { Id = "v", Name = "V", Spikes = level.Spikes });

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single().Spikes[1].OutlineWidth, Is.EqualTo(7f));
        Assert.That(read.Variants.Single().Spikes[1].OutlineColor, Is.EqualTo(0x11223344u));
    }

    [Test]
    public void SpikesSavedBeforeOutlinesExistedGetTheDefault()
    {
        LevelDocument level = MapWithSpikes();
        level.Spikes[1].OutlineWidth = 7f;
        byte[] current = XmfBinary.Write(level);

        // Rebuild the file without the spike outline chunk, as an older map was saved.
        ChunkReader reader = ChunkReader.Open(current, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");
        var file = new ChunkWriter("XMF1"u8.ToArray(), XmfBinary.MajorVersion, 0);
        for (uint id = 1; id <= 12; id++)
        {
            if (reader.TryGet(id, out Chunk chunk))
                file.Add(id, chunk.ToArray());
        }

        LevelDocument map = XmfBinary.Read(file.ToArray());

        Assert.That(map.Spikes, Has.Count.EqualTo(2));
        Assert.That(map.Spikes[1].OutlineWidth, Is.EqualTo(Block.DefaultOutlineWidth));
        Assert.That(map.Spikes[1].OutlineColor, Is.EqualTo(Block.DefaultOutlineColor.ToRgba32()));
    }
}
