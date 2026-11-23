using Xanadu.Model.Settings;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Input;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;
using Xanadu.Screens.Editor;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The editor's key list outgrew its panel as tools were added. It must clip
/// and scroll inside the panel instead of running off the screen.
/// </summary>
public sealed class EditorHelpSceneTest : SceneTest
{
    private EditorOverlay? _overlay;

    public override string Name => "Editor key list";
    public override string Slug => "editor-help";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  !EditorPanel.Find(Context.Entities, EditorPanelKind.Bindings).IsNull);
        AddEditorSong();

        AddStep("create the editor overlay", () =>
        {
            _overlay = new EditorOverlay(
                Context.Entities,
                Context.Pointer,
                () => Context.Scenes.TryBack(),
                () => Context.Scenes.TryForward(),
                openSongDialog: false);
            Context.Overlays.Add(_overlay);
        });
        AddStep("open the key list", () =>
        {
            // The host hands keys to whatever takes them: the overlay tagged the editor when it opened.
            Entity editor = default;
            foreach (Entity entity in Context.Entities.Query().AllTags(Tags.Get<KeyTarget>()).Entities)
                editor = entity;
            if (editor.IsNull)
                throw new SceneTestAssertionException("the overlay did not take the editor's keys");

            editor.EmitSignal(new KeyPressed(new KeyChord(GameDebugKeys.EditorHelp)));
        });
        AddUntilStep("the key list shows", () =>
        {
            Entity panel = EditorPanel.Find(Context.Entities, EditorPanelKind.Bindings);
            return !panel.IsNull && !UiLayout.IsHidden(panel);
        });
        AddWaitStep("settle", 2);

        AddAssert("the list scrolls inside its panel", () =>
        {
            Entity viewport = default;
            foreach (Entity entity in Context.Entities.Query<ScrollOffset>().ToEntityList())
                viewport = entity;

            if (viewport.IsNull || viewport.ChildCount == 0)
                return false;

            Entity content = viewport.ChildEntities[0];
            float shown = viewport.GetComponent<Rectangle>().Bounds.Size.Y;
            float list = content.GetComponent<Rectangle>().Bounds.Size.Y;
            return list > shown;
        });
    }
}
