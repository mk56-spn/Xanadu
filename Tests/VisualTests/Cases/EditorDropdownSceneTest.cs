using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Editor;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;
using Writer.Systems.Playfields;

namespace Writer.Tests.VisualTests;

/// <summary>Regression: the ECS snap and note-type dropdowns must be clickable.</summary>
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
                  VisualAssert.HasText(Context.Entities, "SNAP") &&
                  VisualAssert.HasText(Context.Entities, "CHARACTER") &&
                  Context.Entities.Query<Dropdown>().Count == 2);
        AddAssert(
            "note swatches use lane colours",
            NoteSwatchesAreColoured);
        AddStep("record closed sidebar", () =>
        {
            _closedSidebarHeight = SidebarHeight();
            _closedSidebarTop = SidebarTop();
        });

        AddStep("click snap dropdown", () => ClickDropdown(EditorDropdownKind.Snap));
        AddWaitStep("hold snap dropdown", 2);
        AddStep("release snap dropdown", () => Context.Pointer.Release());
        AddAssert(
            "snap dropdown opened",
            () => FindDropdown(EditorDropdownKind.Snap).GetComponent<Dropdown>().Open);
        AddAssert(
            "snap does not resize the right sidebar",
            () => Math.Abs(SidebarHeight() - _closedSidebarHeight) < 0.1f);
        AddAssert(
            "sidebar top stays fixed when open",
            () => SidebarTop() == _closedSidebarTop);

        AddStep(
            "click character while snap is open",
            () => ClickDropdown(EditorDropdownKind.Character));
        AddWaitStep("hold character over snap", 2);
        AddStep("release character over snap", () => Context.Pointer.Release());
        AddUntilStep(
            "character replaces snap",
            () => FindDropdown(EditorDropdownKind.Character)
                      .GetComponent<Dropdown>().Open &&
                  !FindDropdown(EditorDropdownKind.Snap)
                      .GetComponent<Dropdown>().Open);
        AddAssert(
            "character list is aligned after snap closes",
            () => DropdownListIsAligned(EditorDropdownKind.Character));

        AddStep(
            "reopen snap over character",
            () => ClickDropdown(EditorDropdownKind.Snap));
        AddWaitStep("hold snap over character", 2);
        AddStep("release snap over character", () => Context.Pointer.Release());
        AddUntilStep(
            "snap replaces character",
            () => FindDropdown(EditorDropdownKind.Snap)
                      .GetComponent<Dropdown>().Open &&
                  !FindDropdown(EditorDropdownKind.Character)
                      .GetComponent<Dropdown>().Open);

        AddStep("choose 1/16 snap", () => ClickDropdownOption(EditorDropdownKind.Snap, 2));
        AddWaitStep("hold snap option", 2);
        AddStep("release snap option", () => Context.Pointer.Release());
        AddUntilStep(
            "snap changed",
            () => Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().SnapDivision == 16);
        AddAssert(
            "sidebar collapses when closed",
            () => SidebarHeight() == _closedSidebarHeight);

        AddStep("click R3 swatch", () => ClickSwatch(KeybindAction.R3));
        AddWaitStep("hold swatch", 2);
        AddStep("release swatch", () => Context.Pointer.Release());
        AddUntilStep(
            "note type changed",
            () => Context.Entities.Query<EditorNoteTarget>().ToEntityList()[0]
                .GetComponent<EditorNoteTarget>().Target == KeybindAction.R3);

        AddStep("click mine tool", () => ClickTool(EditorPlacementMode.Mines));
        AddWaitStep("hold mine tool", 2);
        AddStep("release mine tool", () => Context.Pointer.Release());
        AddUntilStep(
            "swatches hide outside note mode",
            () => SwatchPosition() == UiLayout.Hidden);

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
        foreach (Entity entity in Context.Entities.Query<EditorNoteSwatch>().ToEntityList())
        {
            count++;
            if (!entity.HasComponent<ButtonVisual>())
                return false;
        }

        return count == KeybindSettings.Actions.Length;
    }

    private Vector2 SwatchPosition()
    {
        foreach (Entity entity in Context.Entities.Query<EditorNoteSwatch>().ToEntityList())
            return entity.GetComponent<Position>().Value;

        return default;
    }

    private void ClickSwatch(KeybindAction action)
    {
        foreach (Entity entity in Context.Entities.Query<EditorNoteSwatch>().ToEntityList())
        {
            if (entity.GetComponent<EditorNoteSwatch>().Action != action)
                continue;

            Context.Pointer.Click(entity.GetComponent<Position>().Value);
            return;
        }
    }

    private void ClickTool(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorPlacementButton>().ToEntityList())
        {
            if (entity.GetComponent<EditorPlacementButton>().Mode != mode)
                continue;

            Context.Pointer.Click(entity.GetComponent<Position>().Value);
            return;
        }
    }

    private bool DropdownListIsAligned(EditorDropdownKind kind)
    {
        Entity button = FindDropdown(kind);
        Dropdown dropdown = button.GetComponent<Dropdown>();
        if (!dropdown.Open || dropdown.List.IsNull)
            return false;

        Vector2 buttonPosition = button.GetComponent<Position>().Value;
        float buttonHeight = button.GetComponent<Rectangle>().Bounds.Size.Y;
        Vector2 expected = buttonPosition + new Vector2(
            0,
            buttonHeight / 2 + dropdown.ListSize.Y / 2 + 4);
        return dropdown.List.GetComponent<Position>().Value == expected;
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
            : panel.GetComponent<Position>().Value.Y +
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
        Context.Pointer.Click(button.GetComponent<Position>().Value);
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
}
