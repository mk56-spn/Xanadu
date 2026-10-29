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

public sealed class EditorMetadataSceneTest : SceneTest
{
    public override string Name => "Editor metadata";
    public override string Slug => "editor-metadata";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "SNAP"));

        AddStep("open song menu", () =>
            Context.Pointer.Click(Center(ByText("SONG ▾"))));
        AddWaitStep("hold song menu", 2);
        AddStep("release song menu", () => Context.Pointer.Release());
        AddUntilStep(
            "song menu opens",
            () => ByText("SONG ▾").GetComponent<Dropdown>().Open);

        AddStep("click level info", () =>
            Context.Pointer.Click(Center(ByText("LEVEL INFO"))));
        AddWaitStep("hold level info", 2);
        AddStep("release level info", () => Context.Pointer.Release());
        AddUntilStep(
            "wait for metadata panel",
            () => Context.Entities.Query<EditorMetadataPanel>().ToEntityList()[0]
                .GetComponent<EditorMetadataPanel>().Visible);
        AddAssert(
            "metadata fields exist",
            () => Context.Entities.Query<EditorField>().Count == 8);
        AddAssert(
            "start property panel is separate",
            () => Context.Entities.Query<EditorStartPanel>().Count == 1 &&
                  !Context.Entities.Query<EditorStartPanel>()
                      .ToEntityList()[0]
                      .GetComponent<EditorStartPanel>().Visible);

        AddStep("set map background", () =>
            Context.Systems.Commands.Enqueue(
                Xanadu.Editor.Commands.EditorCommand.SetBackgroundColor(
                    new Color("#204060"))));
        AddUntilStep(
            "background reaches the level",
            () => Context.Entities.Query<LevelInfo>().ToEntityList()
                .Any(level => level.GetComponent<LevelInfo>().Background ==
                    new Color("#204060")));
        AddAssert(
            "background swatch matches",
            () => Context.Entities.Query<EditorBackgroundSwatch, FillColor>()
                .ToEntityList()
                .Any(swatch =>
                    swatch.GetComponent<FillColor>().Value ==
                    new Color("#204060")));
        AddAssert(
            "background hex label matches",
            () => Context.Entities.Query<EditorBackgroundHex, Text>()
                .ToEntityList()
                .Any(hex => hex.GetComponent<Text>().Value == "#204060"));

        AddStep("click level name field", () =>
        {
            foreach (Entity entity in Context.Entities
                         .Query<EditorField>()
                         .ToEntityList())
            {
                if (entity.GetComponent<EditorField>().Kind !=
                    EditorFieldKind.LevelName)
                {
                    continue;
                }

                Vector2 center = UiTransform.WorldPosition(
                    entity,
                    entity.GetComponent<Position>().Value);
                Context.Pointer.Click(center);
                return;
            }
        });
        AddWaitStep("hold level name field", 2);
        AddStep("release level name field", () => Context.Pointer.Release());
        AddUntilStep(
            "level name field focused",
            () => Context.Entities.Query<TextField>()
                .AllTags(Tags.Get<TextFieldFocused>())
                .Count == 1);

        AddStep("click beat offset field", () =>
        {
            foreach (Entity entity in Context.Entities
                         .Query<EditorField>()
                         .ToEntityList())
            {
                if (entity.GetComponent<EditorField>().Kind !=
                    EditorFieldKind.Offset)
                {
                    continue;
                }

                Context.Pointer.Click(UiTransform.WorldPosition(
                    entity,
                    entity.GetComponent<Position>().Value));
                return;
            }
        });
        AddWaitStep("hold beat offset field", 2);
        AddStep("release beat offset field", () => Context.Pointer.Release());
        AddUntilStep(
            "beat offset field focused",
            () => Context.Entities.Query<TextField>()
                .AllTags(Tags.Get<TextFieldFocused>())
                .ToEntityList()
                .Any(field => field.HasComponent<EditorField>() &&
                    field.GetComponent<EditorField>().Kind ==
                    EditorFieldKind.Offset));

        AddStep("create and select a start point", () =>
        {
            Entity editor = Context.Entities.Query<PlayfieldView>()
                .ToEntityList()[0];
            Entity start = Context.Entities.CreateEntity(new StartPoint(
                "Test",
                2_000,
                new Vector2(500, 500),
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1)));
            editor.GetComponent<NoteSelection>().Note = start;
        });
        AddUntilStep(
            "wait for start panel",
            () => Context.Entities.Query<EditorStartPanel>().ToEntityList()[0]
                .GetComponent<EditorStartPanel>().Visible);

        AddStep("click start time field", () =>
        {
            foreach (Entity entity in Context.Entities
                         .Query<EditorField>()
                         .ToEntityList())
            {
                if (entity.GetComponent<EditorField>().Kind !=
                    EditorFieldKind.StartTime)
                {
                    continue;
                }

                Context.Pointer.Click(UiTransform.WorldPosition(
                    entity,
                    entity.GetComponent<Position>().Value));
                return;
            }
        });
        AddWaitStep("hold start time field", 2);
        AddStep("release start time field", () => Context.Pointer.Release());
        AddUntilStep(
            "start time field focused",
            () => Context.Entities.Query<TextField>()
                .AllTags(Tags.Get<TextFieldFocused>())
                .Count == 1);
    }

    private Entity ByText(string value) =>
        Context.Entities.Query<Text>().ToEntityList()
            .First(entity => entity.GetComponent<Text>().Value == value);

    private static Vector2 Center(Entity entity) =>
        UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value);
}
