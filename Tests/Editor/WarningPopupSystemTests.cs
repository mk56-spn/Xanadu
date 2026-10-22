using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Editor;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class WarningPopupSystemTests
{
    private EntityStore _store = null!;
    private Entity _editor;
    private Entity _popup;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _editor = _store.CreateEntity(
            new LevelInfo("Test", ""),
            new Playfield(1000, 1000),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
        _popup = EditorWarningPopup.Build(_store, new FakeVisualRidFactory());
        _root = new SystemRoot(_store);
        _root.Add(new WarningPopupSystem(_store));
    }

    [Test]
    public void RequestShowsPopupWithMessageAtPlayfieldCenter()
    {
        EditorWarningPopup.Request(_editor, "No song");

        _root.Update(new UpdateTick(1f / 60f, 0));

        WarningPopup popup = _popup.GetComponent<WarningPopup>();
        Assert.That(popup.Visible, Is.True);
        Assert.That(popup.Message.GetComponent<Text>().Value, Is.EqualTo("No song"));
        Assert.That(
            _popup.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(500, 500)));
        Assert.That(_editor.HasComponent<WarningRequest>(), Is.False);
    }

    [Test]
    public void PressingDismissButtonHidesPopup()
    {
        EditorWarningPopup.Request(_editor, "No song");
        _root.Update(new UpdateTick(1f / 60f, 0));

        _popup.GetComponent<WarningPopup>().Button.AddTag<Pressed>();

        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_popup.GetComponent<WarningPopup>().Visible, Is.False);
        Assert.That(
            _popup.GetComponent<Position>().Value,
            Is.EqualTo(EditorWarningPopup.HiddenPosition));
    }

    [Test]
    public void LoadingSongHidesPopup()
    {
        EditorWarningPopup.Request(_editor, "No song");
        _root.Update(new UpdateTick(1f / 60f, 0));

        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();
        level.AudioPath = "/tmp/song.wav";

        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_popup.GetComponent<WarningPopup>().Visible, Is.False);
    }

    private sealed class FakeVisualRidFactory : IVisualRidFactory
    {
        public Rid CreateCanvasItem() => default;
    }
}
