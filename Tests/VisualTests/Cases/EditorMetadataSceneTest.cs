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
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.VisualTests;

public sealed class EditorMetadataSceneTest : SceneTest
{
    // The real editor runs with its overlay, which also reads UI requests; without
    // it here, the overlay swallowing LEVEL INFO's toggle went unnoticed.
    private EditorOverlay? _overlay;

    public override string Name => "Editor metadata";
    public override string Slug => "editor-metadata";

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

        AddStep("open the menu", () =>
            Context.Pointer.Click(Center(ByText("MENU ▾"))));
        AddWaitStep("hold the menu", 2);
        AddStep("release the menu", () => Context.Pointer.Release());
        AddUntilStep(
            "the menu opens",
            () => ByText("MENU ▾").GetComponent<Dropdown>().Open);

        AddStep("click level info", () =>
            Context.Pointer.Click(Center(ByText("LEVEL INFO"))));
        AddWaitStep("hold level info", 2);
        AddStep("release level info", () => Context.Pointer.Release());
        AddUntilStep(
            "wait for metadata panel",
            () => !UiLayout.IsHidden(EditorPanel.Find(Context.Entities, EditorPanelKind.Metadata)));
        AddAssert(
            "metadata fields exist",
            () => Context.Entities.Query<EditorField>().WithDisabled().Count == 6);
        AddAssert(
            "the song is changed from level info",
            () => Context.Entities.Query<EditorSongLabel>().Count == 1 &&
                  Context.Entities.Query<EditorButton>().ToEntityList()
                      .Any(button => button.GetComponent<EditorButton>().Kind == EditorButtonKind.LoadSong));
        AddAssert(
            "start property panel is separate",
            () => EditorPanel.Find(Context.Entities, EditorPanelKind.Start) is { IsNull: false } start &&
                  UiLayout.IsHidden(start));

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
            () => ColorRows(EditorColorTarget.Background, hex: false)
                .Any(swatch =>
                    swatch.GetComponent<FillColor>().Value ==
                    new Color("#204060")));
        AddAssert(
            "background hex label matches",
            () => ColorRows(EditorColorTarget.Background, hex: true)
                .Any(hex => hex.GetComponent<Text>().Value == "#204060"));

        AddStep("set the colour outside the playfield", () =>
            Context.Systems.Commands.Enqueue(
                Xanadu.Editor.Commands.EditorCommand.SetOutsideColor(
                    new Color("#3a1030"))));
        AddUntilStep(
            "outside colour reaches the level",
            () => Context.Entities.Query<LevelInfo>().ToEntityList()
                .Any(level => level.GetComponent<LevelInfo>().Outside ==
                    new Color("#3a1030")));
        AddAssert(
            "outside swatch and hex match",
            () => ColorRows(EditorColorTarget.Outside, hex: false)
                      .Any(swatch => swatch.GetComponent<FillColor>().Value == new Color("#3a1030")) &&
                  ColorRows(EditorColorTarget.Outside, hex: true)
                      .Any(hex => hex.GetComponent<Text>().Value == "#3a1030"));

        AddStep("set the playfield's frame colour", () =>
            Context.Systems.Commands.Enqueue(
                Xanadu.Editor.Commands.EditorCommand.SetFrameColor(
                    new Color("#e0a020"))));
        AddUntilStep(
            "frame colour reaches the level",
            () => Context.Entities.Query<LevelInfo>().ToEntityList()
                .Any(level => level.GetComponent<LevelInfo>().Frame ==
                    new Color("#e0a020")));
        AddAssert(
            "frame swatch and hex match",
            () => ColorRows(EditorColorTarget.Frame, hex: false)
                      .Any(swatch => swatch.GetComponent<FillColor>().Value == new Color("#e0a020")) &&
                  ColorRows(EditorColorTarget.Frame, hex: true)
                      .Any(hex => hex.GetComponent<Text>().Value == "#e0a020"));

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

        AddStep("click artist field", () =>
        {
            foreach (Entity entity in Context.Entities
                         .Query<EditorField>()
                         .ToEntityList())
            {
                if (entity.GetComponent<EditorField>().Kind !=
                    EditorFieldKind.Artist)
                {
                    continue;
                }

                Context.Pointer.Click(UiTransform.WorldPosition(
                    entity,
                    entity.GetComponent<Position>().Value));
                return;
            }
        });
        AddWaitStep("hold artist field", 2);
        AddStep("release artist field", () => Context.Pointer.Release());
        AddUntilStep(
            "artist field focused",
            () => Context.Entities.Query<TextField>()
                .AllTags(Tags.Get<TextFieldFocused>())
                .ToEntityList()
                .Any(field => field.HasComponent<EditorField>() &&
                    field.GetComponent<EditorField>().Kind ==
                    EditorFieldKind.Artist));

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
            () => !UiLayout.IsHidden(EditorPanel.Find(Context.Entities, EditorPanelKind.Start)));

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
        AddStep("close editor overlay", () =>
        {
            _overlay?.Close();
            _overlay = null;
        });
    }

    private Entity ByText(string value) =>
        Context.Entities.Query<Text>().ToEntityList()
            .First(entity => entity.GetComponent<Text>().Value == value);

    private static Vector2 Center(Entity entity) =>
        UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value);

    private System.Collections.Generic.List<Entity> ColorRows(EditorColorTarget target, bool hex) =>
        Context.Entities.Query<EditorColorRow>().ToEntityList()
            .Where(entity =>
                entity.GetComponent<EditorColorRow>().Target == target &&
                entity.GetComponent<EditorColorRow>().Hex == hex)
            .ToList();
}
