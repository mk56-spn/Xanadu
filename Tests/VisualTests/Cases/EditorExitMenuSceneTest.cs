using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Regression: EXIT in the editor's MENU dropdown must leave the editor when
/// driven by real clicks, from opening the list to answering the confirmation.
/// </summary>
public sealed class EditorExitMenuSceneTest : SceneTest
{
    private EditorOverlay? _overlay;

    public override string Name => "Editor exit menu";
    public override string Slug => "editor-exit-menu";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "MENU ▾"));
        AddStep("create editor overlay", () =>
        {
            _overlay = new EditorOverlay(
                Context.Entities,
                Context.Systems.Commands,
                Context.Pointer,
                () => Context.Scenes.TryBack(),
                () => Context.Scenes.TryForward(),
                openSongDialog: false);
            Context.Host.AddChild(new EditorOverlayNode(_overlay));
        });
        AddWaitStep("overlay settles", 2);

        AddStep("click MENU", () => ClickText("MENU ▾"));
        AddWaitStep("hold MENU", 2);
        AddStep("release MENU", () => Context.Pointer.Release());
        AddWaitStep("list opens", 2);
        AddAssert("MENU list is open", () => Owner("MENU ▾").GetComponent<Dropdown>().Open);

        AddStep("click EXIT", ClickExit);
        AddWaitStep("hold EXIT", 2);
        AddStep("release EXIT", () => Context.Pointer.Release());
        AddUntilStep("confirmation is showing", ConfirmationIsShowing);

        AddStep("click YES", () => ClickNode(Confirmation().Yes));
        AddWaitStep("hold YES", 2);
        AddStep("release YES", () => Context.Pointer.Release());
        AddUntilStep(
            "the editor is gone",
            () => Context.Scenes.Current != SceneKind.Editor);
        AddStep("close overlay", () =>
        {
            _overlay?.Close();
            _overlay = null;
        });
    }

    private bool ConfirmationIsShowing()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
            return !UiLayout.IsHidden(entity);
        return false;
    }

    private SceneConfirmation Confirmation()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
            return entity.GetComponent<SceneConfirmation>();
        throw new SceneTestAssertionException("confirmation panel missing");
    }

    private Entity Owner(string label)
    {
        foreach (Entity entity in Context.Entities.Query<Dropdown, Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == label)
                return entity;
        }

        throw new SceneTestAssertionException($"no dropdown labelled {label}");
    }

    private void ClickText(string label) => ClickNode(Owner(label));

    private void ClickExit()
    {
        foreach (Entity entity in Context.Entities
                     .Query<SceneCommand>()
                     .WithDisabled()
                     .ToEntityList())
        {
            if (entity.GetComponent<SceneCommand>().Action == SceneActionId.Exit)
            {
                ClickNode(entity);
                return;
            }
        }

        throw new SceneTestAssertionException("EXIT option missing");
    }

    private void ClickNode(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value));
}
