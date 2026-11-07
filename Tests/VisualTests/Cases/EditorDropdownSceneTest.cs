using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
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
    private float _closedSidebarHeight;
    private float _closedSidebarTop;

    public override string Name => "Editor dropdowns";
    public override string Slug => "editor-dropdowns";

    protected override void SetUpSteps()
    {
        AddStep("open editor dropdown scene", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "CHARACTER") &&
                  !FindDropdown(EditorDropdownKind.Character).IsNull);
        AddAssert(
            "note swatches use lane colours",
            NoteSwatchesAreColoured);
        AddStep("record closed sidebar", () =>
        {
            _closedSidebarHeight = SidebarHeight();
            _closedSidebarTop = SidebarTop();
        });

        AddStep("click character dropdown", () => ClickDropdown(EditorDropdownKind.Character));
        AddWaitStep("hold character dropdown", 2);
        AddStep("release character dropdown", () => Context.Pointer.Release());
        AddUntilStep(
            "character dropdown opened",
            () => FindDropdown(EditorDropdownKind.Character).GetComponent<Dropdown>().Open);
        AddAssert(
            "sidebar top stays fixed when open",
            () => SidebarTop() == _closedSidebarTop);
        AddAssert(
            "character list is aligned",
            () => DropdownListIsAligned(EditorDropdownKind.Character));

        AddStep("choose splitline", () => ClickDropdownOption(EditorDropdownKind.Character, (int)CharacterKind.SplitLine));
        AddWaitStep("hold character option", 2);
        AddStep("release character option", () => Context.Pointer.Release());
        AddUntilStep(
            "character changed",
            () => Context.Entities.Query<EditorPlacement>().ToEntityList()[0]
                .GetComponent<EditorPlacement>().Character == CharacterKind.SplitLine);
        AddAssert(
            "sidebar collapses when closed",
            () => SidebarHeight() == _closedSidebarHeight);

        AddStep("click R3 swatch", () => ClickSwatch(KeybindAction.R3));
        AddWaitStep("hold swatch", 2);
        AddStep("release swatch", () => Context.Pointer.Release());
        AddUntilStep(
            "note type changed",
            () => Context.Entities.Query<EditorPlacement>().ToEntityList()[0]
                .GetComponent<EditorPlacement>().Note == KeybindAction.R3);

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

        AddStep("click zoom toolbar", () => ClickEditorButton(EditorButtonKind.ZoomIn));
        AddWaitStep("hold zoom toolbar", 2);
        AddStep("release zoom toolbar", () => Context.Pointer.Release());
        AddUntilStep(
            "toolbar zoom changed",
            () => Context.Entities.Query<TimelineView>().ToEntityList()[0]
                .GetComponent<TimelineView>().Zoom >= 32f);
    }

    private bool NoteSwatchesAreColoured()
    {
        int count = 0;
        foreach (Entity entity in EditorButton.All(Context.Entities, EditorButtonKind.NoteType))
        {
            count++;
            if (!entity.HasComponent<ButtonVisual>())
                return false;
        }

        return count == KeybindSettings.Actions.Length;
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

    private bool DropdownListIsAligned(EditorDropdownKind kind)
    {
        Entity button = FindDropdown(kind);
        Dropdown dropdown = button.GetComponent<Dropdown>();
        if (!dropdown.Open || dropdown.List.IsNull)
            return false;

        Vector2 buttonPosition = WorldPosition(button, button.GetComponent<Position>().Value);
        float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
        Vector2 expected = buttonPosition + new Vector2(
            0,
            buttonHeight / 2 + dropdown.ListSize.Y / 2 + 4);
        Vector2 list = WorldPosition(dropdown.List, dropdown.List.GetComponent<Position>().Value);
        return list.DistanceTo(expected) < 0.5f;
    }

    private float SidebarHeight()
    {
        Entity panel = SidebarPanel();
        return panel.IsNull
            ? 0
            : panel.GetComponent<Rectangle>().Bounds.Size.Y;
    }

    private float SidebarTop()
    {
        Entity panel = SidebarPanel();
        return panel.IsNull
            ? 0
            : WorldPosition(panel, panel.GetComponent<Position>().Value).Y +
              panel.GetComponent<Rectangle>().Bounds.Position.Y;
    }

    private Entity SidebarPanel()
    {
        foreach (Entity editor in Context.Entities
                     .Query<EditorSidebarLayout>()
                     .ToEntityList())
        {
            return editor.GetComponent<EditorSidebarLayout>().Panel;
        }

        return default;
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

    private void ClickDropdown(EditorDropdownKind kind)
    {
        Entity button = FindDropdown(kind);
        Context.Pointer.Click(WorldPosition(button, button.GetComponent<Position>().Value));
    }

    private void ClickDropdownOption(EditorDropdownKind kind, int value)
    {
        Entity button = FindDropdown(kind);
        foreach (Entity option in Context.Entities
                     .Query<DropdownOption>()
                     .ToEntityList())
        {
            DropdownOption data = option.GetComponent<DropdownOption>();
            if (data.Owner.Id != button.Id || data.Value != value)
                continue;

            Context.Pointer.Click(WorldPosition(
                option,
                option.GetComponent<Position>().Value));
            return;
        }
    }

    private Entity FindDropdown(EditorDropdownKind kind)
    {
        foreach (Entity entity in Context.Entities
                     .Query<EditorDropdownControl>()
                     .ToEntityList())
        {
            if (entity.GetComponent<EditorDropdownControl>().Kind != kind)
                continue;

            return entity;
        }

        return default;
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
