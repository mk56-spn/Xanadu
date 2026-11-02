using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: a selected note or mine keeps its outline once the playhead has
/// scrolled past the point where its body fades out.
/// </summary>
public sealed class EditorSelectionOutlineSceneTest : SceneTest
{
    private const long ItemTimeMs = 0;
    private const long ScrolledTimeMs = 5_000;

    private Entity _note;
    private Entity _mine;

    public override string Name => "Editor selection outline";
    public override string Slug => "editor-selection-outline";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<NoteSelection, Playhead>().Count == 1);
        AddStep("select a note and a mine", () =>
        {
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            _note = Context.Entities.CreateEntity(
                new Note(ItemTimeMs, view.ScreenToPlayfield(new Vector2(700, 500))));
            _mine = Context.Entities.CreateEntity(
                new Mine(ItemTimeMs, view.ScreenToPlayfield(new Vector2(880, 500))));
            Editor().GetComponent<NoteSelection>().SetMany([_note, _mine]);
        });
        AddStep("scroll past both", () =>
            Editor().GetComponent<Playhead>().TimeMs = ScrolledTimeMs);
        AddWaitStep("let the editor redraw", 3);
        AddAssert(
            "both bodies have faded out",
            () => NoteFade.Alpha(_note.GetComponent<Note>(), Time()) <= 0 &&
                  MineFade.Alpha(_mine.GetComponent<Mine>(), Time()) <= 0);
        AddAssert(
            "both are still selected",
            () => Editor().GetComponent<NoteSelection>().Contains(_note) &&
                  Editor().GetComponent<NoteSelection>().Contains(_mine));
    }

    private long Time() => Editor().GetComponent<Playhead>().TimeMs;

    private Entity Editor() =>
        Context.Entities.Query<NoteSelection, Playhead>().ToEntityList()[0];
}
