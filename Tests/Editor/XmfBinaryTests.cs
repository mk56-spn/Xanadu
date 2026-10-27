using System;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfBinaryTests
{
    [Test]
    public void RoundTripsEveryPartOfTheMap()
    {
        var source = new LevelDocument
        {
            Version = 3,
            Id = "level-7",
            Slug = "map-test",
            SongId = "song-9",
            Name = "Map Test",
            SnapDivision = 8,
            PlayfieldWidth = 900,
            PlayfieldHeight = 800,
            PlayerShader = 2,
            FieldShader = 1,
        };
        source.Notes.Add(new NoteDocument
        {
            Groups = { 1, 3 },
            Shader = 1,
            TimeMs = 1_234,
            X = 10,
            Y = 20,
            Target = KeybindAction.R3,
            Kind = NoteKind.Contact,
            DashDirection = DashDirection.DownLeft,
            ContactRequired = 4,
            Character = CharacterKind.Mage,
        });
        source.Mines.Add(new MineDocument
        {
            Groups = { 2 },
            Shader = 2,
            TimeMs = 77,
            X = 1,
            Y = 2,
            FadeInMs = 300,
            FadeOutMs = 400,
        });
        source.Blocks.Add(new BlockDocument
        {
            Groups = { 4 },
            Shader = 1,
            X = 5,
            Y = 6,
            Width = 700,
            Height = 80,
        });
        source.StartPoints.Add(new StartPointDocument
        {
            Groups = { 1 },
            Name = "Chorus",
            TimeMs = 9_000,
            X = 100,
            Y = 200,
            MomentumX = 3,
            MomentumY = -4,
            DirectionX = 0.5f,
            DirectionY = -0.5f,
        });
        source.Groups.Add(new GroupDocument
        {
            Id = 1,
            Name = "Lights",
            Effects =
            {
                new GroupEffectDocument
                {
                    Kind = GroupEffectKind.Color,
                    Start = 1.5f,
                    End = 3f,
                    Ease = GroupEase.InOut,
                    Muted = true,
                    X = 2,
                    Y = 3,
                    Tint = "#ff00ff80",
                },
            },
        });
        source.Shaders.Add(new ShaderDocument
        {
            Slot = 1,
            Name = "Glow",
            Source = "shader_type canvas_item;\nvoid fragment() {}",
            Params =
            {
                ["tint"] = new[] { 1f, 0.5f, 0f, 1f },
            },
        });

        LevelDocument map = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(map.Version, Is.EqualTo(3));
        Assert.That(map.Id, Is.EqualTo("level-7"));
        Assert.That(map.Slug, Is.EqualTo("map-test"));
        Assert.That(map.SongId, Is.EqualTo("song-9"));
        Assert.That(map.Name, Is.EqualTo("Map Test"));
        Assert.That(map.SnapDivision, Is.EqualTo(8));
        Assert.That(map.PlayfieldWidth, Is.EqualTo(900));
        Assert.That(map.PlayfieldHeight, Is.EqualTo(800));
        Assert.That(map.PlayerShader, Is.EqualTo(2));
        Assert.That(map.FieldShader, Is.EqualTo(1));

        NoteDocument note = map.Notes[0];
        Assert.That(note.Groups, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(note.Shader, Is.EqualTo(1));
        Assert.That(note.TimeMs, Is.EqualTo(1_234));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.R3));
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Contact));
        Assert.That(note.DashDirection, Is.EqualTo(DashDirection.DownLeft));
        Assert.That(note.ContactRequired, Is.EqualTo(4));
        Assert.That(note.Character, Is.EqualTo(CharacterKind.Mage));

        MineDocument mine = map.Mines[0];
        Assert.That(mine.Groups, Is.EqualTo(new[] { 2 }));
        Assert.That(mine.Shader, Is.EqualTo(2));
        Assert.That(mine.TimeMs, Is.EqualTo(77));
        Assert.That(mine.FadeInMs, Is.EqualTo(300));
        Assert.That(mine.FadeOutMs, Is.EqualTo(400));

        BlockDocument block = map.Blocks[0];
        Assert.That(block.Groups, Is.EqualTo(new[] { 4 }));
        Assert.That(block.Width, Is.EqualTo(700));
        Assert.That(block.Height, Is.EqualTo(80));

        StartPointDocument start = map.StartPoints[0];
        Assert.That(start.Name, Is.EqualTo("Chorus"));
        Assert.That(start.MomentumY, Is.EqualTo(-4));
        Assert.That(start.DirectionX, Is.EqualTo(0.5f));
        Assert.That(start.DirectionY, Is.EqualTo(-0.5f));

        GroupDocument group = map.Groups[0];
        Assert.That(group.Id, Is.EqualTo(1));
        Assert.That(group.Name, Is.EqualTo("Lights"));
        GroupEffectDocument effect = group.Effects[0];
        Assert.That(effect.Kind, Is.EqualTo(GroupEffectKind.Color));
        Assert.That(effect.End, Is.EqualTo(3f));
        Assert.That(effect.Ease, Is.EqualTo(GroupEase.InOut));
        Assert.That(effect.Muted, Is.True);
        Assert.That(effect.Tint, Is.EqualTo("#ff00ff80"));

        ShaderDocument shader = map.Shaders[0];
        Assert.That(shader.Slot, Is.EqualTo(1));
        Assert.That(shader.Name, Is.EqualTo("Glow"));
        Assert.That(shader.Source, Does.Contain("shader_type canvas_item"));
        Assert.That(shader.Params["tint"], Is.EqualTo(new[] { 1f, 0.5f, 0f, 1f }));
    }

    [Test]
    public void RejectsSomethingThatIsNotAMap()
    {
        Assert.Throws<XmfFormatException>(() =>
            XmfBinary.Read("NOPE"u8.ToArray()));
    }

    [Test]
    public void RejectsAnUnsupportedMajorVersion()
    {
        byte[] map = XmfBinary.Write(new LevelDocument());
        map[4] = 9;

        Assert.Throws<XmfFormatException>(() => XmfBinary.Read(map));
    }

    [Test]
    public void RejectsATruncatedMap()
    {
        byte[] map = XmfBinary.Write(new LevelDocument());

        Assert.Throws<XmfFormatException>(() =>
            XmfBinary.Read(map[..^8]));
    }

    [Test]
    public void RejectsAMapWithoutAllOfItsChunks()
    {
        var file = new ChunkWriter("XMF1"u8.ToArray(), XmfBinary.MajorVersion, 0);
        file.Add(1, new StringTable("XMF").Write());

        Assert.Throws<XmfFormatException>(() => XmfBinary.Read(file.ToArray()));
    }

    [Test]
    public void RejectsAGroupSlotOutsideTheLevel()
    {
        var level = new LevelDocument();
        level.Groups.Add(new GroupDocument { Id = 99 });

        Assert.Throws<XmfFormatException>(() =>
            XmfBinary.Read(XmfBinary.Write(level)));
    }

    [Test]
    public void RejectsANoteWithAnUnknownKind()
    {
        var level = new LevelDocument();
        level.Notes.Add(new NoteDocument { Kind = (NoteKind)99 });

        Assert.Throws<XmfFormatException>(() =>
            XmfBinary.Read(XmfBinary.Write(level)));
    }
}
