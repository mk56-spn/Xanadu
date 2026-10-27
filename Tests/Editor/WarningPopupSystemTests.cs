using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class WarningPopupSystemTests : EcsSystemTest
{
    private Entity _editor;
    private Entity _popup;

    protected override void OnEcsSetUp()
    {
        _editor = Store.CreateEntity(
            new LevelInfo("Test", ""),
            new Playfield(1000, 1000),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
        _popup = EditorWarningPopup.Build(Store, new FakeVisualRidFactory());
        Root.Add(new WarningPopupSystem(Store));
    }

    [Test]
    public void RequestShowsPopupWithMessageAtPlayfieldCenter()
    {
        EditorWarningPopup.Request(_editor, "No song");

        Frame();

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
        Frame();

        _popup.GetComponent<WarningPopup>().Button.AddTag<Pressed>();

        Frame();

        Assert.That(_popup.GetComponent<WarningPopup>().Visible, Is.False);
        Assert.That(
            _popup.GetComponent<Position>().Value,
            Is.EqualTo(EditorWarningPopup.HiddenPosition));
    }

    [Test]
    public void LoadingSongHidesPopup()
    {
        EditorWarningPopup.Request(_editor, "No song");
        Frame();

        ref LevelInfo level = ref _editor.GetComponent<LevelInfo>();
        level.AudioPath = "/tmp/song.wav";

        Frame();

        Assert.That(_popup.GetComponent<WarningPopup>().Visible, Is.False);
    }


}
