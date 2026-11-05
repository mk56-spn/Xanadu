using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Text written into a level, in the real editor: labels at several sizes, one
/// tinted, the selected one boxed, and the sidebar's text controls resizing it
/// through a real click.
/// </summary>
public sealed class EditorTextSceneTest : SceneTest
{
    private Entity _selected;

    public override string Name => "Editor text";
    public override string Slug => "editor-text";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorTextPanel>().Count == 1);

        AddStep("write text into the level", () =>
        {
            Entity editor = Editor();
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            EditorItems.Create(Context.Entities, new LevelText(At(700, 260), "VERSE ONE", 48f / view.Scale));
            Entity tinted = EditorItems.Create(Context.Entities, new LevelText(At(700, 420), "Hold the beat", 72f / view.Scale));
            SelfModulate.Put(tinted, new Color("#ffb040"));
            _selected = EditorItems.Create(Context.Entities, new LevelText(At(700, 600), "small cue", 28f / view.Scale));
            editor.GetComponent<NoteSelection>().Note = _selected;
        });
        AddUntilStep("the text controls show", () =>
            Editor().GetComponent<EditorTextPanel>().Section.Enabled);

        float before = 0f;
        AddStep("click SIZE +", () =>
        {
            before = _selected.GetComponent<LevelText>().Size;
            Click(SizeButton(1));
        });
        AddWaitStep("hold +", 2);
        AddStep("release +", () => Context.Pointer.Release());
        AddUntilStep("the text grew", () =>
            _selected.GetComponent<LevelText>().Size > before);
        AddAssert("the field shows its words", () =>
            Editor().GetComponent<EditorTextPanel>().Field.GetComponent<TextField>().Value == "small cue");
    }

    private Entity SizeButton(int delta)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.TextSize))
        {
            if (button.GetComponent<EditorButton>().Arg == delta)
                return button;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private Entity Editor() => Context.Entities.Query<EditorTextPanel>().ToEntityList()[0];
}
