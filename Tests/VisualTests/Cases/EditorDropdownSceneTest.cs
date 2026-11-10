using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

/// <summary>Regression: the ECS character dropdown and note-type swatches must be clickable.</summary>
public sealed class EditorDropdownSceneTest : SceneTest
{
    public override string Name => "Editor dropdowns";
    public override string Slug => "editor-dropdowns";

    protected override void SetUpSteps()
    {
        AddStep("open editor dropdown scene", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, UiCollapsibleControl.Header("SELECTION", expanded: true)));
        AddEditorSong();
        AddAssert(
            "note swatches use action colours",
            NoteSwatchesAreColoured);

        AddStep("click action 3 swatch", () => ClickSwatch(KeybindAction.Action3));
        AddWaitStep("hold swatch", 2);
        AddStep("release swatch", () => Context.Pointer.Release());
        AddUntilStep(
            "note type changed",
            () => Context.Entities.Query<EditorPlacement>().ToEntityList()[0]
                .GetComponent<EditorPlacement>().Note == KeybindAction.Action3);

        AddStep("open the spikes and mines group", () => ClickGroupOf(EditorPlacementMode.Mines));
        AddWaitStep("hold the spikes and mines group", 2);
        AddStep("release the spikes and mines group", () => Context.Pointer.Release());
        AddUntilStep("the spikes and mines flyout opens", () => GroupIsOpen(EditorPlacementMode.Mines));
        AddStep("click mine tool", () => ClickTool(EditorPlacementMode.Mines));
        AddWaitStep("hold mine tool", 2);
        AddStep("release mine tool", () => Context.Pointer.Release());
        AddUntilStep(
            "the note key bar hides for mines",
            () => Context.Entities.Query<EditorNoteBarPanel>().Count == 0);

        AddStep("click play toolbar", () => ClickEditorButton(EditorButtonKind.Play));
        AddWaitStep("hold play toolbar", 2);
        AddStep("release play toolbar", () => Context.Pointer.Release());
        AddUntilStep(
            "toolbar play started",
            () => Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().Playing);

        float zoomBefore = 0f;
        AddStep("click zoom toolbar", () =>
        {
            zoomBefore = TimelineZoomNow();
            ClickEditorButton(EditorButtonKind.ZoomIn);
        });
        AddWaitStep("hold zoom toolbar", 2);
        AddStep("release zoom toolbar", () => Context.Pointer.Release());
        AddUntilStep("toolbar zoom changed", () => TimelineZoomNow() > zoomBefore);
    }

    private float TimelineZoomNow() =>
        Context.Entities.Query<TimelineView>().ToEntityList()[0].GetComponent<TimelineView>().Zoom;

    private bool NoteSwatchesAreColoured()
    {
        int count = 0;
        foreach (Entity entity in EditorButton.All(Context.Entities, EditorButtonKind.NoteType))
        {
            count++;
            if (!entity.HasComponent<ButtonVisual>())
                return false;
        }

        return count == KeybindSettings.ActionKeys.Length;
    }

    private Vector2 NoteBarPosition()
    {
        foreach (Entity entity in Context.Entities.Query<EditorNoteBarPanel>().ToEntityList())
            return entity.GetComponent<Position>().Value;

        return default;
    }

    private void ClickSwatch(KeybindAction action)
    {
        foreach (Entity entity in EditorButton.All(Context.Entities, EditorButtonKind.NoteType))
        {
            if ((KeybindAction)entity.GetComponent<EditorButton>().Arg != action)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity, entity.GetComponent<Position>().Value));
            return;
        }
    }

    private void ClickTool(EditorPlacementMode mode)
    {
        foreach (Entity entity in EditorButton.All(Context.Entities, EditorButtonKind.Place))
        {
            if ((EditorPlacementMode)entity.GetComponent<EditorButton>().Arg != mode)
                continue;

            Context.Pointer.Click(WorldPosition(entity, entity.GetComponent<Position>().Value));
            return;
        }
    }

    private void ClickEditorButton(EditorButtonKind kind)
    {
        foreach (Entity entity in Context.Entities.Query<EditorButton>().ToEntityList())
        {
            if (entity.GetComponent<EditorButton>().Kind != kind)
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(
                entity,
                entity.GetComponent<Position>().Value));
            return;
        }
    }

    private static Vector2 WorldPosition(Entity entity, Vector2 local)
    {
        Vector2 world = local;
        Entity parent = entity.Parent;

        while (!parent.IsNull)
        {
            if (parent.TryGetComponent<Position>(out var position))
                world += position.Value;

            parent = parent.Parent;
        }

        return world;
    }
    /// <summary>Presses the strip button of the group holding <paramref name="mode"/>, to open its flyout.</summary>
    private void ClickGroupOf(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            if (!EditorToolGroups.Holds(EditorToolGroups.All[entity.GetComponent<EditorToolGroup>().Index], mode))
                continue;

            Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));
            return;
        }
    }

    private bool GroupIsOpen(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            EditorToolGroup group = entity.GetComponent<EditorToolGroup>();
            if (EditorToolGroups.Holds(EditorToolGroups.All[group.Index], mode))
                return group.Open && !UiLayout.IsHidden(group.Flyout);
        }

        return false;
    }
}
