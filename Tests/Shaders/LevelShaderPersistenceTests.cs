using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class LevelShaderPersistenceTests
{
    private const string Glow = "shader_type canvas_item;\nuniform float speed = 1.0;\nvoid fragment() { COLOR = vec4(1.0); }";

    private EntityStore _store = null!;
    private Entity _editor;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _editor = _store.CreateEntity(
            new LevelInfo("Untitled", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            LevelGroups.Create(),
            LevelShaders.Create());
    }

    [Test]
    public void ShadersAndTheirAssignmentsSurviveALoadAndSave()
    {
        var level = new LevelDocument { Id = "x", Name = "Shaded", PlayerShader = 2, FieldShader = 1 };
        level.Shaders.Add(new ShaderDocument { Slot = 1, Name = "Glow", Source = Glow, Params = { ["speed"] = [0.5f] } });
        level.Shaders.Add(new ShaderDocument { Slot = 2, Name = "Wave", Source = Glow });
        level.Notes.Add(new NoteDocument { TimeMs = 100, X = 1, Y = 2, Shader = 2 });
        level.Mines.Add(new MineDocument { TimeMs = 200, X = 3, Y = 4, Shader = 1 });
        level.Blocks.Add(new BlockDocument { X = 5, Y = 6, Width = 10, Height = 10, Shader = 2 });

        LevelDocumentMapper.Apply(_store, _editor, level);
        LevelDocument saved = LevelDocumentMapper.Capture(_store, _editor);

        Assert.That(saved.Shaders.Select(s => (s.Slot, s.Name)), Is.EqualTo(new[] { (1, "Glow"), (2, "Wave") }));
        Assert.That(saved.Shaders[0].Params["speed"], Is.EqualTo(new[] { 0.5f }));
        Assert.That(saved.Shaders[0].Source, Is.EqualTo(Glow));
        Assert.That(saved.PlayerShader, Is.EqualTo(2));
        Assert.That(saved.FieldShader, Is.EqualTo(1));
        Assert.That(saved.Notes.Single().Shader, Is.EqualTo(2));
        Assert.That(saved.Mines.Single().Shader, Is.EqualTo(1));
        Assert.That(saved.Blocks.Single().Shader, Is.EqualTo(2));
    }

    [Test]
    public void ItemsWithoutAShaderStayWithoutOne()
    {
        var level = new LevelDocument();
        level.Notes.Add(new NoteDocument { TimeMs = 100 });

        LevelDocumentMapper.Apply(_store, _editor, level);

        Assert.That(_store.Query<Note>().ToEntityList().Single().HasComponent<ItemShader>(), Is.False);
        Assert.That(LevelDocumentMapper.Capture(_store, _editor).Notes.Single().Shader, Is.EqualTo(0));
    }

    [Test]
    public void LoadingALevelReplacesTheShadersOfTheOneBefore()
    {
        var first = new LevelDocument();
        first.Shaders.Add(new ShaderDocument { Slot = 3, Name = "Old", Source = Glow });
        LevelDocumentMapper.Apply(_store, _editor, first);

        LevelDocumentMapper.Apply(_store, _editor, new LevelDocument());

        Assert.That(LevelDocumentMapper.Capture(_store, _editor).Shaders, Is.Empty);
    }

    [Test]
    public void ALevelFileRoundTripsThroughJson()
    {
        var level = new LevelDocument { Id = "json", PlayerShader = 1 };
        level.Shaders.Add(new ShaderDocument { Slot = 1, Name = "Glow", Source = Glow, Params = { ["tint"] = [1f, 0.5f, 0f, 1f] } });
        level.Notes.Add(new NoteDocument { Shader = 1 });

        LevelDocument read = LevelSerializer.Deserialize(LevelSerializer.Serialize(level));

        Assert.That(read.Shaders.Single().Params["tint"], Is.EqualTo(new[] { 1f, 0.5f, 0f, 1f }));
        Assert.That(read.Notes.Single().Shader, Is.EqualTo(1));
        Assert.That(read.PlayerShader, Is.EqualTo(1));
    }

    [Test]
    public void NormalizeDropsShadersALevelMayNotCarryAndTheReferencesToThem()
    {
        var level = new LevelDocument { PlayerShader = 2, FieldShader = 3 };
        level.Shaders.Add(new ShaderDocument { Slot = 1, Source = Glow });
        level.Shaders.Add(new ShaderDocument { Slot = 2, Source = "shader_type spatial;\nvoid fragment() {}" });
        level.Shaders.Add(new ShaderDocument { Slot = 1, Source = Glow, Name = "duplicate" });
        level.Shaders.Add(new ShaderDocument { Slot = 11, Source = Glow });
        level.Shaders.Add(new ShaderDocument { Slot = 4, Source = "" });
        level.Notes.Add(new NoteDocument { Shader = 1 });
        level.Notes.Add(new NoteDocument { Shader = 2 });
        level.Notes.Add(new NoteDocument { Shader = 99 });

        LevelDocument clean = LevelSerializer.Normalize(level);

        Assert.That(clean.Shaders.Select(s => s.Slot), Is.EqualTo(new[] { 1 }));
        Assert.That(clean.Shaders[0].Name, Is.Not.EqualTo("duplicate"));
        Assert.That(clean.PlayerShader, Is.EqualTo(0));
        Assert.That(clean.FieldShader, Is.EqualTo(0));
        Assert.That(clean.Notes.Select(n => n.Shader), Is.EqualTo(new[] { 1, 0, 0 }));
    }

    [TestCase("shader_type canvas_item;\nvoid fragment(){}", true)]
    [TestCase("// my glow\n/* block */\nshader_type canvas_item ;\nvoid fragment(){}", true)]
    [TestCase("  \n\nshader_type   canvas_item;", true)]
    [TestCase("shader_type spatial;", false)]
    [TestCase("shader_type particles;", false)]
    [TestCase("void fragment(){}", false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("uniform float x;\nshader_type canvas_item;", false)]
    public void OnlyCanvasShadersAreAcceptable(string source, bool expected)
    {
        Assert.That(ShaderSourceRules.IsAcceptable(source, out string problem), Is.EqualTo(expected));
        Assert.That(problem.Length == 0, Is.EqualTo(expected));
    }

    [Test]
    public void OversizedSourceIsRefused()
    {
        string huge = "shader_type canvas_item;\n" + new string('/', ShaderLimits.MaxSourceChars);

        Assert.That(ShaderSourceRules.IsAcceptable(huge, out _), Is.False);
    }
}
