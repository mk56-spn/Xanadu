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
                  VisualAssert.HasText(Context.Entities, "NOTE TYPE") &&
                  Context.Entities.Query<Dropdown>().Count == 3);
        AddAssert(
            "note type options use lane colours",
            NoteTypeOptionsAreColoured);
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
            "click note type while snap is open",
            () => ClickDropdown(EditorDropdownKind.NoteType));
        AddWaitStep("hold type over snap", 2);
        AddStep("release type over snap", () => Context.Pointer.Release());
        AddUntilStep(
            "type replaces snap",
            () => FindDropdown(EditorDropdownKind.NoteType)
                      .GetComponent<Dropdown>().Open &&
                  !FindDropdown(EditorDropdownKind.Snap)
                      .GetComponent<Dropdown>().Open);
        AddAssert(
            "type list is aligned after snap closes",
            () => DropdownListIsAligned(EditorDropdownKind.NoteType));

        AddStep(
            "reopen snap over type",
            () => ClickDropdown(EditorDropdownKind.Snap));
        AddWaitStep("hold snap over type", 2);
        AddStep("release snap over type", () => Context.Pointer.Release());
        AddUntilStep(
            "snap replaces type",
            () => FindDropdown(EditorDropdownKind.Snap)
                      .GetComponent<Dropdown>().Open &&
                  !FindDropdown(EditorDropdownKind.NoteType)
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

        AddStep("click note type dropdown", () => ClickDropdown(EditorDropdownKind.NoteType));
        AddWaitStep("hold note type dropdown", 2);
        AddStep("release note type dropdown", () => Context.Pointer.Release());
        AddAssert(
            "note type dropdown opened",
            () => FindDropdown(EditorDropdownKind.NoteType).GetComponent<Dropdown>().Open);

        AddStep(
            "choose R3 note type",
            () => ClickDropdownOption(EditorDropdownKind.NoteType, (int)KeybindAction.R3));
        AddWaitStep("hold note type option", 2);
        AddStep("release note type option", () => Context.Pointer.Release());
        AddUntilStep(
            "note type changed",
            () => Context.Entities.Query<EditorNoteTarget>().ToEntityList()[0]
                .GetComponent<EditorNoteTarget>().Target == KeybindAction.R3);

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

    private bool NoteTypeOptionsAreColoured()
    {
        foreach (Entity entity in Context.Entities
                     .Query<DropdownOption>()
                     .ToEntityList())
        {
            DropdownOption option = entity.GetComponent<DropdownOption>();
            if (option.Owner.IsNull ||
                !option.Owner.HasComponent<EditorDropdownControl>() ||
                option.Owner.GetComponent<EditorDropdownControl>().Kind !=
                EditorDropdownKind.NoteType)
            {
                continue;
            }

            Color expected = PlayfieldRenderer.ColorFor(
                KeybindSettings.Actions[option.Value]);
            if (entity.GetComponent<FillColor>().Value != expected)
                return false;
        }

        return true;
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
