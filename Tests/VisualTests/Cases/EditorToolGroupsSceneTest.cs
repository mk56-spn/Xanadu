using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The tool strip's groups in the real editor: the notes button opens its
/// flyout beside the strip, and a start point beside a note is drawn and
/// picked the note's size.
/// </summary>
public sealed class EditorToolGroupsSceneTest : SceneTest
{
    private Entity _start;

    public override string Name => "Editor tool groups";
    public override string Slug => "editor-tool-groups";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the tool groups",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorToolGroup>().Count > 0);
        AddEditorSong();

        AddStep("put a note and a start point side by side", () =>
        {
            PlayfieldView view = Context.Entities.Query<EditorPlacement, PlayfieldView>()
                .ToEntityList()[0].GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            EditorItems.Create(Context.Entities, new Note(0, At(620, 500)));
            _start = EditorItems.Create(Context.Entities, new StartPoint(
                "", 0, At(760, 500), System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        });

        AddStep("click the start point's edge", () =>
        {
            PlayfieldView view = Context.Entities.Query<EditorPlacement, PlayfieldView>()
                .ToEntityList()[0].GetComponent<PlayfieldView>();
            float radius = Xanadu.Systems.Playfields.PlayfieldRenderer.StartPointRadius(view);
            Context.Pointer.Click(new Vector2(760 + radius * 0.8f, 500));
        });
        AddWaitStep("hold", 2);
        AddStep("release", () => Context.Pointer.Release());
        AddUntilStep("the start point is picked at a note's reach", () =>
            Context.Entities.Query<EditorPlacement, NoteSelection>().ToEntityList()[0]
                .GetComponent<NoteSelection>().Contains(_start));

        AddStep("open the notes group", () =>
        {
            Entity notes = NotesGroup();
            Context.Pointer.Click(UiTransform.WorldPosition(notes, notes.GetComponent<Position>().Value));
        });
        AddWaitStep("hold", 2);
        AddStep("release", () => Context.Pointer.Release());
        AddUntilStep("the notes flyout is open", () =>
        {
            EditorToolGroup group = NotesGroup().GetComponent<EditorToolGroup>();
            return group.Open && !UiLayout.IsHidden(group.Flyout);
        });
        AddWaitStep("settle", 4);
    }

    /// <summary>The strip's first button, which holds the note tools.</summary>
    private Entity NotesGroup()
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            if (entity.GetComponent<EditorToolGroup>().Index == 0)
                return entity;
        }

        return default;
    }
}
