using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;

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

        var hard = new XmfVariant { Id = "hard", Name = "Hard", FieldShader = 1 };
        hard.Notes.Add(new NoteDocument
        {
            TimeMs = 50,
            X = 3,
            Y = 4,
            DashStrength = DashStrength.Double,
        });
        hard.Notes.Add(new NoteDocument { TimeMs = 150, X = 5, Y = 6 });
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Id, Is.EqualTo("mapset-1"));
        Assert.That(read.SongId, Is.EqualTo("song-9"));
        Assert.That(read.Shaders.Single().Name, Is.EqualTo("Glow"));
        Assert.That(read.Variants, Has.Count.EqualTo(2));

        XmfVariant readEasy = read.Variants.Single(v => v.Id == "easy");
        Assert.That(readEasy.Name, Is.EqualTo("Easy"));
        Assert.That(readEasy.PlayerShader, Is.EqualTo(1));
        Assert.That(readEasy.Notes.Single().TimeMs, Is.EqualTo(100));
        Assert.That(readEasy.Notes.Single().DashStrength, Is.EqualTo(DashStrength.Single));

        XmfVariant readHard = read.Variants.Single(v => v.Id == "hard");
        Assert.That(readHard.FieldShader, Is.EqualTo(1));
        Assert.That(readHard.Notes, Has.Count.EqualTo(2));
        Assert.That(readHard.Notes[0].DashStrength, Is.EqualTo(DashStrength.Double));
        Assert.That(readHard.Notes[1].DashStrength, Is.EqualTo(DashStrength.Single));
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
