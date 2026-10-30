using System;
using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Storage;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class XmfBinaryTests
{
    [Test]
    public void ANotesPolygonItemKeepsItsOutlineAndFill()
    {
        var polygon = new XifPrimitive
        {
            Kind = XifPrimitive.PolygonKind,
            Points = { new Godot.Vector2(0, 0), new Godot.Vector2(10, 0), new Godot.Vector2(5, 8) },
            Fill = new Godot.Color(1f, 0.5f, 0f, 0.75f),
        };
        var source = new LevelDocument();
        source.Notes.Add(new NoteDocument { Item = new Xif { Primitives = { polygon } } });
        source.Notes.Add(new NoteDocument { DashStrength = DashStrength.Double });

        LevelDocument loaded = XmfBinary.Read(XmfBinary.Write(source));

        XifPrimitive read = loaded.Notes[0].Item.Primitives.Single();
        Assert.That(read.Points, Is.EqualTo(polygon.Points));
        Assert.That(read.Fill, Is.EqualTo(polygon.Fill));
        Assert.That(loaded.Notes[1].DashStrength, Is.EqualTo(DashStrength.Double));
    }

    [Test]
    public void ALevelWithoutItemGeometryIsWrittenAsBefore()
    {
        var source = new LevelDocument();
        source.Notes.Add(new NoteDocument
        {
            Item = new Xif { Primitives = { new XifPrimitive { Kind = "svg", Ref = "res://lantern.svg" } } },
        });
        byte[] plain = XmfBinary.Write(source);

        source.Notes[0].Item.Primitives[0].Fill = new Godot.Color(1f, 0f, 0f);
        byte[] tinted = XmfBinary.Write(source);

        // One byte per note of strength, then one point count and four fill floats per primitive.
        Assert.That(tinted.Length - plain.Length, Is.EqualTo(1 + 4 + 16));
    }

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
            BackgroundColor = "#204060",
            PlayerShader = 2,
            FieldShader = 1,
        };
        source.Notes.Add(new NoteDocument
        {
            Groups = { 1, 3 },
            Item = new Xif
            {
                Id = "lantern",
                Name = "Lantern",
                Owner = XifOwner.Game,
                Primitives = { new XifPrimitive { Kind = "svg", Ref = "res://lantern.svg" } },
                Shaders =
                {
                    XifShaderRef.OfMap(1),
                    XifShaderRef.OfCommon("res://Assets/Shaders/Items/rainbow.gdshader"),
                },
                Params = { ["glow"] = new[] { 0.5f } },
            },
            TimeMs = 1_234,
            X = 10,
            Y = 20,
            Target = KeybindAction.R3,
            Kind = NoteKind.Contact,
            DashDirection = DashDirection.DownLeft,
            ContactRequired = 4,
            Character = CharacterKind.Form,
            DashStrength = DashStrength.Triple,
            PairIndex = PairIndex.Two,
            Momentum = 900f,
            ContactLeft = 2,
            ContactRight = 1,
            ChannelSide = ChannelSide.Right,
            HoldMs = 500,
            ShapeId = 8,
            ShapePiece = 2,
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
            ShapeId = 8,
            ShapePiece = 1,
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
        Assert.That(map.BackgroundColor, Is.EqualTo("#204060"));
        Assert.That(map.PlayerShader, Is.EqualTo(2));
        Assert.That(map.FieldShader, Is.EqualTo(1));

        NoteDocument note = map.Notes[0];
        Assert.That(note.Groups, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(note.Item.Id, Is.EqualTo("lantern"));
        Assert.That(note.Item.Name, Is.EqualTo("Lantern"));
        Assert.That(note.Item.Owner, Is.EqualTo(XifOwner.Game));
        XifPrimitive primitive = note.Item.Primitives.Single();
        Assert.That(primitive.Kind, Is.EqualTo("svg"));
        Assert.That(primitive.Ref, Is.EqualTo("res://lantern.svg"));
        Assert.That(note.Item.PrimaryShader, Is.EqualTo(1));
        XifShaderRef commonShader = note.Item.Shaders.Single(s => s.Scope == XifShaderScope.Common);
        Assert.That(commonShader.Path, Is.EqualTo("res://Assets/Shaders/Items/rainbow.gdshader"));
        Assert.That(note.Item.Params["glow"], Is.EqualTo(new[] { 0.5f }));
        Assert.That(note.TimeMs, Is.EqualTo(1_234));
        Assert.That(note.Target, Is.EqualTo(KeybindAction.R3));
        Assert.That(note.Kind, Is.EqualTo(NoteKind.Contact));
        Assert.That(note.DashDirection, Is.EqualTo(DashDirection.DownLeft));
        Assert.That(note.ContactRequired, Is.EqualTo(4));
        Assert.That(note.Character, Is.EqualTo(CharacterKind.Form));
        Assert.That(note.DashStrength, Is.EqualTo(DashStrength.Triple));
        Assert.That(note.PairIndex, Is.EqualTo(PairIndex.Two));
        Assert.That(note.Momentum, Is.EqualTo(900f));
        Assert.That(note.ContactLeft, Is.EqualTo(2));
        Assert.That(note.ContactRight, Is.EqualTo(1));
        Assert.That(note.ChannelSide, Is.EqualTo(ChannelSide.Right));
        Assert.That(note.HoldMs, Is.EqualTo(500));
        Assert.That(note.ShapeId, Is.EqualTo(8));
        Assert.That(note.ShapePiece, Is.EqualTo(2));

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
        Assert.That(block.ShapeId, Is.EqualTo(8));
        Assert.That(block.ShapePiece, Is.EqualTo(1));

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
    public void MapsWrittenBeforeBackgroundsExistedStillLoad()
    {
        byte[] current = XmfBinary.Write(new LevelDocument { Name = "Old" });

        // Rebuild the file with the trailing background index cut off the meta
        // chunk, as it is in a map saved before maps carried a background.
        ChunkReader reader = ChunkReader.Open(
            current,
            "XMF1"u8.ToArray(),
            XmfBinary.MajorVersion,
            "XMF");
        var file = new ChunkWriter(
            "XMF1"u8.ToArray(),
            XmfBinary.MajorVersion,
            0);
        for (uint id = 1; id <= 9; id++)
        {
            if (!reader.TryGet(id, out Chunk chunk))
                continue;

            byte[] payload = chunk.ToArray();
            if (id == 2)
                payload = payload[..^sizeof(uint)];
            file.Add(id, payload);
        }

        LevelDocument map = XmfBinary.Read(file.ToArray());

        Assert.That(map.Name, Is.EqualTo("Old"));
        Assert.That(map.BackgroundColor, Is.Empty);
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
