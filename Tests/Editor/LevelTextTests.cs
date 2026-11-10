using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class LevelTextTests : EcsSystemTest
{
    private EditorCommandBus _commands = null!;
    private EditorHistory _history = null!;
    private Entity _editor;

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        _history = new EditorHistory(Store);
        Root.Add(new EditorCommandSystem(Store, _commands, _history));
        _editor = Store.CreateEntity(
            new LevelInfo("Untitled"),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorMetronome(false),
            new NoteSelection(default),
            LevelGroups.Create());
    }

    [Test]
    public void TextIsCapturedAndAppliedWithItsColourAndGroups()
    {
        Entity text = EditorItems.Create(Store, new LevelText(new Vector2(200, 300), "Hello", 80f));
        SelfModulate.Put(text, new Color("#ff8000"));
        text.AddComponent(new ItemGroups(ItemGroups.Bit(3)));

        LevelDocument document = LevelDocumentMapper.Capture(Store, _editor);
        TextDocument saved = document.Texts.Single();
        Assert.That(saved.Text, Is.EqualTo("Hello"));
        Assert.That(saved.Size, Is.EqualTo(80f));
        Assert.That(saved.Groups, Is.EqualTo(new[] { 3 }));

        var targetStore = new EntityStore();
        Entity target = targetStore.CreateEntity(
            new LevelInfo("Target"),
            new Playfield(10, 10),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default));
        LevelDocumentMapper.Apply(targetStore, target, document);

        Entity restored = targetStore.Query<LevelText>().ToEntityList().Single();
        LevelText applied = restored.GetComponent<LevelText>();
        Assert.That(applied.Text, Is.EqualTo("Hello"));
        Assert.That(applied.Position, Is.EqualTo(new Vector2(200, 300)));
        Assert.That(applied.Size, Is.EqualTo(80f));
        Assert.That(SelfModulate.Of(restored), Is.EqualTo(new Color("#ff8000")));
        Assert.That(restored.GetComponent<ItemGroups>().Mask, Is.EqualTo(ItemGroups.Bit(3)));
    }

    [Test]
    public void UndoAndRedoBringTextBackAsItWas()
    {
        EditorSnapshot before = _history.Capture();
        EditorItems.Create(Store, new LevelText(new Vector2(10, 20), "Cue", 40f));
        _history.Record(before, _history.Capture());

        Assert.That(_history.Undo(), Is.True);
        Assert.That(Store.Query<LevelText>().Count, Is.Zero);

        Assert.That(_history.Redo(), Is.True);
        LevelText text = Store.Query<LevelText>().ToEntityList().Single().GetComponent<LevelText>();
        Assert.That(text.Text, Is.EqualTo("Cue"));
        Assert.That(text.Size, Is.EqualTo(40f));
    }

    [Test]
    public void ChangingOnlyTheWordsIsAnUndoStep()
    {
        Entity text = EditorItems.Create(Store, new LevelText(new Vector2(10, 20), "Before"));
        EditorSnapshot before = _history.Capture();
        text.GetComponent<LevelText>().Text = "After";
        _history.Record(before, _history.Capture());

        Assert.That(_history.Undo(), Is.True);
        Assert.That(Store.Query<LevelText>().ToEntityList().Single().GetComponent<LevelText>().Text, Is.EqualTo("Before"));
    }

    [Test]
    public void CopiedTextPastesOneGridCellAlong()
    {
        Entity text = EditorItems.Create(Store, new LevelText(new Vector2(100, 100), "Copy me"));
        _editor.GetComponent<NoteSelection>().Note = text;

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        var texts = Store.Query<LevelText>().ToEntityList().Select(e => e.GetComponent<LevelText>()).ToList();
        Assert.That(texts, Has.Count.EqualTo(2));
        Assert.That(texts.All(t => t.Text == "Copy me"), Is.True);
        Assert.That(texts.Select(t => t.Position).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void TheBoxGrowsWithTheTextAndItsSize()
    {
        var shortText = new LevelText(Vector2.Zero, "Hi", 50f);
        var longText = new LevelText(Vector2.Zero, "Hello there", 50f);
        var bigText = new LevelText(Vector2.Zero, "Hi", 100f);

        Assert.That(longText.Bounds.Size.X, Is.GreaterThan(shortText.Bounds.Size.X));
        Assert.That(bigText.Bounds.Size.Y, Is.GreaterThan(shortText.Bounds.Size.Y));
        Assert.That(shortText.Bounds.GetCenter(), Is.EqualTo(Vector2.Zero));
    }
}
