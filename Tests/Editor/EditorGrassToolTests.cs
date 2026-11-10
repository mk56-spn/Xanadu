using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

/// <summary>The grass tool places a patch, and its panel tunes it and the brush.</summary>
[TestFixture]
public sealed class EditorGrassToolTests
{
    private EntityStore _store = null!;
    private FakePointerInput _pointer = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;
    private Entity _editor;
    private Entity _section;
    private Entity[] _readouts = null!;
    private Entity _color;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, _history));
        _root.Add(new EditorGrassPanelSystem(_history));

        _section = _store.CreateEntity();
        _readouts = System.Enum.GetValues<GrassSetting>().Select(_ => _store.CreateEntity(new Text(""))).ToArray();
        _color = _store.CreateEntity(new UiColorField(default));
        _editor = _store.CreateEntity(
            new LevelInfo("Test", TestSongs.At("/tmp/test.wav")),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default),
            new EditorPlacement { Mode = EditorPlacementMode.Grass },
            new EditorGrassPanel(_section, _readouts, _color));
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Click(Vector2 point)
    {
        _pointer.Current = new PointerInput(point, true);
        Frame();
        _pointer.Current = new PointerInput(point, false);
        Frame();
    }

    private void Press(GrassSetting setting, int sign)
    {
        Entity button = _store.CreateEntity(
            new EditorButton(EditorButtonKind.GrassStep, EditorGrassPanelSystem.StepArg(setting, sign)));
        button.AddTag<Pressed>();
        Frame();
        button.DeleteEntity();
        Frame();
    }

    private Entity OnlyGrass => _store.Query<GrassPatch>().ToEntityList().Single();

    [Test]
    public void ClickingTheFieldPlacesSelectedGrassThere()
    {
        Click(new Vector2(300, 400));

        // Snapped, the patch's left side lands on the nearest column line (200)
        // and its base on a row line, where block edges sit.
        GrassPatch patch = OnlyGrass.GetComponent<GrassPatch>();
        Assert.That(patch.Position, Is.EqualTo(new Vector2(325, 400)));
        Assert.That(patch.Bounds.Position.X, Is.EqualTo(200));
        Assert.That(patch.Width, Is.EqualTo(GrassPatch.DefaultWidth));
        Assert.That(_editor.GetComponent<NoteSelection>().Note, Is.EqualTo(OnlyGrass));
        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void SteppingChangesTheSelectedGrassAndUndoesInOneStep()
    {
        Click(new Vector2(300, 400));

        Press(GrassSetting.Length, 1);

        float longer = GrassPatch.DefaultLength + GrassPatch.Step(GrassSetting.Length);
        Assert.That(OnlyGrass.GetComponent<GrassPatch>().Length, Is.EqualTo(longer));
        Assert.That(_readouts[(int)GrassSetting.Length].GetComponent<Text>().Value, Is.EqualTo($"LENGTH {longer:0}"));
        Assert.That(_history.Undo(), Is.True);
        Assert.That(OnlyGrass.GetComponent<GrassPatch>().Length, Is.EqualTo(GrassPatch.DefaultLength));
    }

    [Test]
    public void TheNextPatchMatchesTheLastOneStepped()
    {
        Click(new Vector2(300, 400));
        Press(GrassSetting.Sway, 1);
        _editor.GetComponent<NoteSelection>().Clear();

        Click(new Vector2(300, 800));

        GrassPatch[] patches = _store.Query<GrassPatch>().ToEntityList()
            .Select(e => e.GetComponent<GrassPatch>()).ToArray();
        Assert.That(patches, Has.Length.EqualTo(2));
        Assert.That(patches.All(p => p.Sway == GrassPatch.DefaultSway + GrassPatch.Step(GrassSetting.Sway)), Is.True);
        Assert.That(patches[0].Seed, Is.Not.EqualTo(patches[1].Seed));
    }

    [Test]
    public void WithNothingSelectedTheButtonsTuneTheBrush()
    {
        Press(GrassSetting.Density, -1);

        Assert.That(
            _editor.GetComponent<EditorPlacement>().Grass.Density,
            Is.EqualTo(GrassPatch.DefaultDensity - GrassPatch.Step(GrassSetting.Density)));
        Assert.That(_store.Query<GrassPatch>().Count, Is.Zero);
    }

    [Test]
    public void ACommittedColourGoesToTheSelectedGrass()
    {
        Click(new Vector2(300, 400));
        _color.AddTag<TextFieldFocused>();
        Frame();
        _color.RemoveTag<TextFieldFocused>();
        ref UiColorField field = ref _color.GetComponent<UiColorField>();
        field.Value = new Color(1f, 0.5f, 0f);
        field.Committed = true;
        Frame();

        Assert.That(OnlyGrass.GetComponent<GrassPatch>().Color, Is.EqualTo(new Color(1f, 0.5f, 0f)));
        Assert.That(_editor.GetComponent<EditorPlacement>().Grass.Color, Is.EqualTo(new Color(1f, 0.5f, 0f)));
    }
}
