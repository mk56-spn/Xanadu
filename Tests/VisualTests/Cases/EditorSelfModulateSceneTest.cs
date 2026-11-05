using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// An item's self modulate in the real editor: the sidebar's colour field shows
/// the selection's colour, typing one tints the selection, and the swatch follows.
/// </summary>
public sealed class EditorSelfModulateSceneTest : SceneTest
{
    private Entity _block;

    public override string Name => "Editor self modulate";
    public override string Slug => "editor-self-modulate";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the colour field",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorSelfModulateField>().Count == 1);

        AddStep("place and select a block", () =>
        {
            // Placed by screen position, so they sit in view whatever the playfield's size.
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            var size = new Vector2(180, 120) / view.Scale;
            EditorItems.Create(Context.Entities, new Block(At(650, 500), size));
            _block = EditorItems.Create(Context.Entities, new Block(At(900, 500), size));
            Entity note = EditorItems.Create(Context.Entities, new Note(0, At(760, 300)));
            SelfModulate.Put(note, new Color("#00ff80"));
            EditorItems.Create(Context.Entities, new Note(0, At(900, 300)));
            Editor().GetComponent<NoteSelection>().Note = _block;
        });
        AddUntilStep("the field shows white", () => Text() == "#ffffff");

        AddStep("focus the field", () => Field().AddTag<TextFieldFocused>());
        AddStep("type a colour", () => Field().GetComponent<TextField>().Value = "#00ff80");
        AddStep("leave the field", () => Field().RemoveTag<TextFieldFocused>());
        AddUntilStep("the block takes the colour", () => SelfModulate.Of(_block) == new Color("#00ff80"));
        AddAssert("the swatch follows", () =>
            Field().GetComponent<UiColorField>().Swatch.GetComponent<FillColor>().Value == new Color("#00ff80"));
    }

    private Entity Editor() => Context.Entities.Query<EditorSelfModulateField>().ToEntityList()[0];

    private Entity Field() => Editor().GetComponent<EditorSelfModulateField>().Field;

    private string Text() => Field().GetComponent<TextField>().Value;
}
