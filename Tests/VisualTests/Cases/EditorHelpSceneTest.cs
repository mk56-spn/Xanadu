using Friflo.Engine.ECS;
using Xanadu.Editor;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;

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

        AddStep("create the editor overlay", () =>
        {
            _overlay = new EditorOverlay(
                Context.Entities,
                Context.Systems.Commands,
                Context.Pointer,
                () => Context.Scenes.TryBack(),
                () => Context.Scenes.TryForward(),
                openSongDialog: false);
            Context.Host.AddChild(_overlay);
        });
        AddStep("open the key list", () =>
        {
            EditorShortcuts.Binding? binding = EditorShortcuts.Find(
                new KeyChord(DebugKeys.EditorHelp),
                _overlay!.ActiveScopes);
            if (binding is null)
                throw new SceneTestAssertionException("no binding opens the key list");

            _overlay.Run(binding.Value);
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
            foreach (Entity entity in Context.Entities.Query<ScrollView>().ToEntityList())
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
