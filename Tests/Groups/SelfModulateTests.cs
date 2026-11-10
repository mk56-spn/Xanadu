using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Groups;

[TestFixture]
public sealed class SelfModulateTests : EcsSystemTest
{
    private static readonly Color Tint = new("#80ff4020");

    [Test]
    public void AnUntintedItemKeepsItsColour()
    {
        Entity item = Store.CreateEntity();
        Assert.That(GroupColor.For(item, Colors.Red), Is.EqualTo(Colors.Red));
    }

    [Test]
    public void SelfModulateMultipliesTheColour()
    {
        Entity item = Store.CreateEntity(new SelfModulate(new Color(0.5f, 1f, 0f, 0.5f)));
        Assert.That(GroupColor.For(item, new Color(1f, 0.5f, 1f, 1f)), Is.EqualTo(new Color(0.5f, 0.5f, 0f, 0.5f)));
    }

    [Test]
    public void AFinishedGroupFadeOverridesIt()
    {
        Entity item = Store.CreateEntity(new SelfModulate(Colors.Red), new GroupColor(Colors.Blue, 1f));
        Assert.That(GroupColor.For(item, Colors.White), Is.EqualTo(Colors.Blue));
    }

    [Test]
    public void AGroupThatHasNotStartedLeavesIt()
    {
        Entity item = Store.CreateEntity(new SelfModulate(Colors.Red), new GroupColor(Colors.Blue, 0f));
        Assert.That(GroupColor.For(item, Colors.White), Is.EqualTo(Colors.Red));
    }

    [TestCase("", true)]
    [TestCase("#ffffff", true)]
    [TestCase("80ff4020", true)]
    [TestCase("#ff", false)]
    [TestCase("nothex", false)]
    public void TypedColoursAreChecked(string typed, bool valid) =>
        Assert.That(SelfModulate.TryParse(typed, out _), Is.EqualTo(valid));

    [Test]
    public void PutSetsAndClearsTheColour()
    {
        Entity item = EditorItems.Create(Store, new Note(0, Vector2.Zero));
        SelfModulate.Put(item, Tint);
        Assert.That(SelfModulate.Of(item), Is.EqualTo(Tint));
        SelfModulate.Put(item, Colors.White);
        Assert.That(SelfModulate.Of(item), Is.EqualTo(Colors.White));
    }

    [Test]
    public void ItemsCanBeRestoredTintedFromInsideAQuery()
    {
        Store.CreateEntity(new FillColor(Colors.White));
        var history = new EditorHistory(Store);
        Entity note = EditorItems.Create(Store, new Note(0, Vector2.Zero));
        EditorSnapshot before = history.Capture();
        SelfModulate.Put(note, Colors.Red);
        history.Record(before, history.Capture());
        history.Undo();

        // Redo rebuilds the tinted note while a query is running, as the editor's commands do.
        Store.Query<FillColor>().ForEachEntity((ref FillColor _, Entity _) => history.Redo());

        foreach (Entity restored in Store.Query<Note>().ToEntityList())
            Assert.That(SelfModulate.Of(restored), Is.EqualTo(Colors.Red));
    }

    private (Entity Field, Entity Editor, Entity Note, EditorHistory History) EditorWithField()
    {
        Entity swatch = Store.CreateEntity(new FillColor(Colors.White));
        Entity field = Store.CreateEntity(new TextField(""), new UiColorField(swatch));
        Entity note = EditorItems.Create(Store, new Note(0, Vector2.Zero));
        Entity editor = Store.CreateEntity(new NoteSelection(note), new EditorSelfModulateField(field));
        var history = new EditorHistory(Store);
        Root.Add(new UiColorFieldSystem());
        Root.Add(new EditorSelfModulateSystem(history));
        return (field, editor, note, history);
    }

    private void Type(Entity field, string value)
    {
        field.AddTag<TextFieldFocused>();
        Frame();
        field.GetComponent<TextField>().Value = value;
        Frame();
        field.RemoveTag<TextFieldFocused>();
        Frame();
        Frame();
    }

    [Test]
    public void TypingColoursTheSelectionAndUndoRestoresIt()
    {
        (Entity field, _, Entity note, EditorHistory history) = EditorWithField();
        Frame();

        Type(field, "#ff0000");
        Assert.That(SelfModulate.Of(note), Is.EqualTo(Colors.Red));
        Assert.That(field.GetComponent<TextField>().Value, Is.EqualTo("#ff0000"));

        Assert.That(history.Undo(), Is.True);
        foreach (Entity restored in Store.Query<Note>().ToEntityList())
            Assert.That(SelfModulate.Of(restored), Is.EqualTo(Colors.White));
        Assert.That(history.Redo(), Is.True);
        foreach (Entity restored in Store.Query<Note>().ToEntityList())
            Assert.That(SelfModulate.Of(restored), Is.EqualTo(Colors.Red));
    }

    [Test]
    public void DraggingThePickerColoursTheSelectionLiveAndUndoesInOneStep()
    {
        (Entity field, _, Entity note, EditorHistory history) = EditorWithField();
        Entity picker = Store.CreateEntity(new UiColorPicker { Color = Colors.White });
        field.GetComponent<UiColorField>().Picker = picker;
        Frame();

        void Drag(Color color)
        {
            ref UiColorPicker state = ref picker.GetComponent<UiColorPicker>();
            state.Dragging = true;
            state.Color = color;
            Frame();
        }

        Drag(Colors.Red);
        Assert.That(SelfModulate.Of(note).IsEqualApprox(Colors.Red), Is.True, "coloured before the drag ends");
        Drag(Colors.Blue);
        Assert.That(SelfModulate.Of(note).IsEqualApprox(Colors.Blue), Is.True);

        picker.GetComponent<UiColorPicker>().Dragging = false;
        Frame();
        Frame();
        Assert.That(SelfModulate.Of(note).IsEqualApprox(Colors.Blue), Is.True);

        Assert.That(history.Undo(), Is.True);
        foreach (Entity restored in Store.Query<Note>().ToEntityList())
            Assert.That(SelfModulate.Of(restored), Is.EqualTo(Colors.White), "one step back to before the drag");
        Assert.That(history.CanUndo, Is.False);
    }

    [Test]
    public void AnInvalidColourChangesNothing()
    {
        (Entity field, _, Entity note, _) = EditorWithField();
        Frame();
        Type(field, "nope");
        Assert.That(SelfModulate.Of(note), Is.EqualTo(Colors.White));
        Assert.That(field.GetComponent<TextField>().Value, Is.EqualTo("#ffffff"));
    }

    [Test]
    public void ClickingAnotherItemToEndTypingColoursTheFirst()
    {
        (Entity field, Entity editor, Entity first, _) = EditorWithField();
        Entity second = EditorItems.Create(Store, new Note(10, Vector2.One));
        Frame();

        field.AddTag<TextFieldFocused>();
        Frame();
        field.GetComponent<TextField>().Value = "#00ff00";
        editor.GetComponent<NoteSelection>().Note = second;
        field.RemoveTag<TextFieldFocused>();
        Frame();
        Frame();

        Assert.That(SelfModulate.Of(first), Is.EqualTo(Colors.Green));
        Assert.That(SelfModulate.Of(second), Is.EqualTo(Colors.White));
    }

    [Test]
    public void SelectingAnotherItemWithoutTypingColoursNothing()
    {
        (Entity field, Entity editor, Entity first, _) = EditorWithField();
        Entity second = EditorItems.Create(Store, new Note(10, Vector2.One));
        SelfModulate.Put(second, Colors.Red);
        Frame();

        field.AddTag<TextFieldFocused>();
        Frame();
        editor.GetComponent<NoteSelection>().Note = second;
        field.RemoveTag<TextFieldFocused>();
        Frame();
        Frame();

        Assert.That(SelfModulate.Of(first), Is.EqualTo(Colors.White));
        Assert.That(SelfModulate.Of(second), Is.EqualTo(Colors.Red));
        Assert.That(field.GetComponent<TextField>().Value, Is.EqualTo("#ff0000"));
    }

    [Test]
    public void LevelFilesKeepItemColours()
    {
        uint red = Colors.Red.ToRgba32();
        var document = new LevelDocument
        {
            Notes = { new NoteDocument { SelfModulate = red }, new NoteDocument() },
            Mines = { new MineDocument { SelfModulate = red } },
            Blocks = { new BlockDocument { SelfModulate = red } },
            Spikes = { new SpikeDocument { SelfModulate = red } },
        };

        LevelDocument loaded = XmfBinary.Read(XmfBinary.Write(document));

        Assert.That(loaded.Notes[0].SelfModulate, Is.EqualTo(red));
        Assert.That(loaded.Notes[1].SelfModulate, Is.EqualTo(SelfModulate.White));
        Assert.That(loaded.Mines[0].SelfModulate, Is.EqualTo(red));
        Assert.That(loaded.Blocks[0].SelfModulate, Is.EqualTo(red));
        Assert.That(loaded.Spikes[0].SelfModulate, Is.EqualTo(red));
    }

    [Test]
    public void UntintedLevelsHaveNoColourChunk()
    {
        var plain = new LevelDocument { Notes = { new NoteDocument() } };
        var tinted = new LevelDocument { Notes = { new NoteDocument { SelfModulate = Colors.Red.ToRgba32() } } };
        Assert.That(XmfBinary.Write(tinted).Length, Is.GreaterThan(XmfBinary.Write(plain).Length));
        Assert.That(XmfBinary.Read(XmfBinary.Write(plain)).Notes[0].SelfModulate, Is.EqualTo(SelfModulate.White));
    }

    private static XafPackage Package(Color tint)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1 });
        package.Attachments.Add(new XafAttachment { SvgPath = "res://a.svg", Group = "a", BoneId = "root", SelfModulate = tint });
        return package;
    }

    [Test]
    public void RigFilesKeepAttachmentColours() =>
        Assert.That(XafBinary.Read(XafBinary.Write(Package(Colors.Red))).Attachments[0].SelfModulate, Is.EqualTo(Colors.Red));

}
