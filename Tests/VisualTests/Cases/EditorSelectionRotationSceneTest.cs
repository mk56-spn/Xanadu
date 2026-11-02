using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A multi-selection in the real editor: the rotate handle sits above it and the
/// header carries the SNAP switch next to GRID.
/// </summary>
public sealed class EditorSelectionRotationSceneTest : SceneTest
{
    public override string Name => "Editor selection rotation";
    public override string Slug => "editor-selection-rotation";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the snap switch",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<Toggle>().AllTags(Tags.Get<ViewportSnapToggle>()).Count == 1);

        AddStep("select a block and two notes", () =>
        {
            Entity editor = Context.Entities.Query<PlayfieldView, NoteSelection>().ToEntityList()[0];
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            Entity block = EditorItems.Create(
                Context.Entities,
                new Block(At(650, 500), new Vector2(180, 90) / view.Scale));
            Entity first = EditorItems.Create(Context.Entities, new Note(0, At(760, 320)));
            Entity second = EditorItems.Create(Context.Entities, new Note(0, At(1000, 420)));
            editor.GetComponent<NoteSelection>().SetMany(new[] { block, first, second });
        });
        AddAssert("the selection has a handle", () =>
            SelectionRotation.TryGetBounds(
                Context.Entities.Query<PlayfieldView, NoteSelection>().ToEntityList()[0]
                    .GetComponent<NoteSelection>().AllNotes,
                out _));
    }
}
