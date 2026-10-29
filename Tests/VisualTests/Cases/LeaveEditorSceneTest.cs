using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

/// <summary>Regression: the leave-editor confirmation must accept a real click.</summary>
public sealed class LeaveEditorSceneTest : SceneTest
{
    private EditorOverlay? _overlay;
    private bool _saved;

    public override string Name => "Leave editor";
    public override string Slug => "leave-editor";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "LEVEL EDITOR"));
        AddStep("create editor overlay", CreateOverlay);
        AddStep("wire editor save", () =>
            Context.Scenes.SaveEditor = () => _saved = true);
        AddWaitStep("overlay settles", 2);
        AddStep("request leave", () => Context.Scenes.TryBack());
        AddUntilStep("leave confirmation is visible", ConfirmationIsVisible);
        AddStep("click SAVE & LEAVE", ClickSave);
        AddWaitStep("hold click", 2);
        AddStep("release click", () => Context.Pointer.Release());
        AddUntilStep(
            "return to song select",
            () => Context.Scenes.Current == SceneKind.SongSelect);
        AddAssert("editor save was requested", () => _saved);
        AddStep("close editor overlay", CloseOverlay);
    }

    private void CreateOverlay()
    {
        _overlay = new EditorOverlay(
            Context.Entities,
            Context.Systems.Commands,
            Context.Pointer,
            () => Context.Scenes.TryBack(),
            () => Context.Scenes.TryForward(),
            openSongDialog: false);

        Context.Host.AddChild(_overlay);
    }

    private bool ConfirmationIsVisible()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
        {
            if (entity.GetComponent<SceneConfirmation>().Visible)
                return true;
        }

        return false;
    }

    private void ClickSave()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
        {
            SceneConfirmation confirmation = entity.GetComponent<SceneConfirmation>();
            if (!confirmation.Visible || confirmation.Save.IsNull)
                continue;

            Entity save = confirmation.Save;
            Vector2 world = UiTransform.WorldPosition(
                save,
                save.GetComponent<Xanadu.Ecs.Position>().Value);
            Context.Pointer.Click(world);
            return;
        }

        throw new SceneTestAssertionException("leave confirmation SAVE button missing");
    }

    private void CloseOverlay()
    {
        _overlay?.Close();
        _overlay = null;
    }
}
