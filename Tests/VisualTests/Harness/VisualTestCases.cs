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

/// <summary>Structural regression cases for the ECS screens.</summary>
public static class SceneTestCatalog
{
    public static IReadOnlyList<SceneTest> All { get; } = new SceneTest[]
    {
        new ProfilesSceneTest(),
        new MainMenuSceneTest(),
        new SongSelectSceneTest(),
        new EditorWaveformSceneTest(),
        new EditorMetadataSceneTest(),
        new SettingsSceneTest(),
        new OffsetWizardSceneTest(),
        new PlaySceneTest(),
        new MovementJudgementSceneTest(),
        new ResultsSceneTest(),
        new RestartSceneTest(),
        new EditorDropdownSceneTest(),
        new LeaveEditorSceneTest(),
    };
}

public sealed class ProfilesSceneTest : SceneTest
{
    public override string Name => "Profiles";
    public override string Slug => "profiles";

    protected override void SetUpSteps()
    {
        AddStep("open profiles", () => Context.Scenes.Start(SceneKind.Profiles));
        AddUntilStep(
            "wait for profiles",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  VisualAssert.HasText(Context.Entities, "WHO'S PLAYING?"));
        AddAssert(
            "new profile button exists",
            () => VisualAssert.HasText(Context.Entities, "NEW PROFILE"));
        AddAssert(
            "profile selector exists",
            () => Context.Entities.Query<ProfileCreate>().Count == 1);
    }
}

public sealed class MainMenuSceneTest : SceneTest
{
    public override string Name => "Main menu";
    public override string Slug => "main";

    protected override void SetUpSteps()
    {
        AddStep("open main menu", () => Context.Scenes.Start(SceneKind.Main));
        AddUntilStep(
            "wait for main menu",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Main, SceneUi.MenuLayer) &&
                  VisualAssert.HasText(Context.Entities, "WRITER"));
        AddAssert(
            "WRITER title exists",
            () => VisualAssert.HasText(Context.Entities, "WRITER"));
        AddAssert(
            "PLAY button exists",
            () => VisualAssert.HasText(Context.Entities, "PLAY"));
        AddAssert(
            "PLAY button is animated",
            () => VisualAssert.HasAnimatedText(Context.Entities, "PLAY"));
        AddAssert(
            "offset wizard button exists",
            () => VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "menu layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.MenuLayer) == 0);
    }
}

public sealed class SongSelectSceneTest : SceneTest
{
    public override string Name => "Song select";
    public override string Slug => "song-select";

    protected override void SetUpSteps()
    {
        AddStep("open song select", () => Context.Scenes.Start(SceneKind.SongSelect));
        AddUntilStep(
            "wait for song select",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.SongSelect,
                SceneUi.MenuLayer) &&
                VisualAssert.HasText(Context.Entities, "SONG SELECT"));
        AddAssert(
            "SONG SELECT title exists",
            () => VisualAssert.HasText(Context.Entities, "SONG SELECT"));
        AddAssert(
            "song select layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.MenuLayer) == 0);
        AddAssert(
            "score box exists",
            () => VisualAssert.HasText(Context.Entities, "SCORES") &&
                  Context.Entities.Query<ScrollView>().Count == 1);
        AddAssert(
            "profile star frame exists",
            () => Context.Entities.Query<UiImage>().Count == 1);
    }
}

public sealed class EditorWaveformSceneTest : SceneTest
{
    public override string Name => "Editor waveform";
    public override string Slug => "editor-waveform";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  VisualAssert.HasText(Context.Entities, "MENU"));
        AddAssert(
            "editor binding help exists",
            () => Context.Entities.Query<EditorBindingsHelp>().Count == 1 &&
                  !Context.Entities.Query<EditorBindingsHelp>()
                      .ToEntityList()[0]
                      .GetComponent<EditorBindingsHelp>().Visible);

        AddStep("select the test track", () =>
        {
            ref LevelInfo level = ref Context.Entities
                .Query<LevelInfo>()
                .ToEntityList()[0]
                .GetComponent<LevelInfo>();
            level.AudioPath = "res://Audio/test-track.wav";
        });
        AddUntilStep(
            "wait for waveform",
            () =>
            {
                Entity editor = Context.Entities.Query<LevelInfo>().ToEntityList()[0];
                WaveformPreview preview = editor.GetComponent<WaveformPreview>();
                return preview.Left.Length > 0 &&
                       preview.DurationMs > 0 &&
                       editor.GetComponent<Playhead>().DurationMs > 0;
            });

        AddAssert(
            "waveform has peaks",
            () => Context.Entities.Query<WaveformPreview>().ToEntityList()[0]
                .GetComponent<WaveformPreview>().Left.Length > 0);
    }
}

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

        AddStep("open metadata panel", () =>
        {
            foreach (Entity button in Context.Entities
                         .Query<EditorButton>()
                         .ToEntityList())
            {
                if (button.GetComponent<EditorButton>().Kind ==
                    EditorButtonKind.ToggleMetadata)
                {
                    button.AddTag<Pressed>();
                    return;
                }
            }
        });
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
}

public sealed class SettingsSceneTest : SceneTest
{
    public override string Name => "Settings keybinds";
    public override string Slug => "settings";

    protected override void SetUpSteps()
    {
        AddStep("open settings", () => Context.Scenes.Start(SceneKind.Settings));
        AddUntilStep(
            "wait for settings ECS",
            () => Context.Scenes.Current == SceneKind.Settings &&
                  VisualAssert.HasLayer(
                      Context.Entities,
                      SceneUi.MenuLayer - 1) &&
                  VisualAssert.HasLayer(
                      Context.Entities,
                      SceneUi.MenuLayer) &&
                  VisualAssert.CountUninitialised(
                      Context.Entities,
                      SceneUi.MenuLayer - 1) == 0 &&
                  VisualAssert.CountUninitialised(
                      Context.Entities,
                      SceneUi.MenuLayer) == 0 &&
                  VisualAssert.HasText(Context.Entities, "L1"));
        AddAssert(
            "SETTINGS title exists",
            () => VisualAssert.HasText(Context.Entities, "SETTINGS"));
        AddAssert(
            "KEYBINDS heading exists",
            () => VisualAssert.HasText(Context.Entities, "KEYBINDS"));
        AddAssert(
            "VSync toggle exists",
            () => Context.Entities.Query<VSyncToggle>().Count == 1);
        AddAssert(
            "VSync state is shown",
            () => VisualAssert.HasText(Context.Entities, "V-SYNC: ON") ||
                  VisualAssert.HasText(Context.Entities, "V-SYNC: OFF"));
        AddAssert(
            "seven keybind rows exist",
            () => Context.Entities.Query<KeybindButton>().Count ==
                  KeybindSettings.Actions.Length);
        AddAssert(
            "content layer is above background",
            ContentLayerIsAboveBackground);
        AddAssert(
            "keybinds are on the content layer",
            KeybindsAreOnContentLayer);
        AddAssert(
            "keybinds are parented above background",
            KeybindsAreParentedToContent);
        AddAssert(
            "keybinds are animated",
            KeybindsAreAnimated);
        AddAssert(
            "keybind rectangles are non-empty",
            KeybindRectanglesAreNonEmpty);
    }

    private bool ContentLayerIsAboveBackground()
    {
        Entity background = VisualAssert.LayerAt(
            Context.Entities,
            SceneUi.MenuLayer - 1);
        Entity content = VisualAssert.LayerAt(
            Context.Entities,
            SceneUi.MenuLayer);

        return !background.IsNull &&
               !content.IsNull &&
               content.GetComponent<Layer>().ZIndex >
               background.GetComponent<Layer>().ZIndex;
    }

    private bool KeybindsAreOnContentLayer()
    {
        foreach (Entity button in Context.Entities.Query<KeybindButton>().ToEntityList())
        {
            if (button.GetComponent<Drawable>().ZIndex != SceneUi.MenuLayer)
                return false;
        }

        return true;
    }

    private bool KeybindsAreParentedToContent()
    {
        Entity content = VisualAssert.LayerAt(
            Context.Entities,
            SceneUi.MenuLayer);

        if (content.IsNull)
            return false;

        foreach (Entity button in Context.Entities.Query<KeybindButton>().ToEntityList())
        {
            if (button.Parent.IsNull || button.Parent.Id != content.Id)
                return false;
        }

        return true;
    }

    private bool KeybindsAreAnimated()
    {
        foreach (Entity button in Context.Entities.Query<KeybindButton>().ToEntityList())
        {
            if (!button.HasComponent<ButtonVisual>())
                return false;
        }

        return true;
    }

    private bool KeybindRectanglesAreNonEmpty()
    {
        foreach (Entity button in Context.Entities.Query<KeybindButton>().ToEntityList())
        {
            Rectangle bounds = button.GetComponent<Rectangle>();
            if (bounds.Bounds.Size.X <= 0 || bounds.Bounds.Size.Y <= 0)
                return false;
        }

        return true;
    }
}

public sealed class OffsetWizardSceneTest : SceneTest
{
    public override string Name => "Offset wizard";
    public override string Slug => "offset-wizard";

    protected override void SetUpSteps()
    {
        AddStep("open offset wizard", () => Context.Scenes.Start(SceneKind.OffsetWizard));
        AddUntilStep(
            "wait for offset wizard",
            () => Context.Scenes.Current == SceneKind.OffsetWizard &&
                  VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "offset instructions exist",
            () => VisualAssert.HasText(
                Context.Entities,
                "Tap SPACE with every click."));
    }
}

public sealed class PlaySceneTest : SceneTest
{
    public override string Name => "Play HUD and field";
    public override string Slug => "play";

    protected override void SetUpSteps()
    {
        AddStep("open synthetic play scene", OpenPlay);

        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                SceneUi.PlayHudLayer) &&
                VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "level title exists",
            () => VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "score HUD exists",
            () => VisualAssert.HasText(Context.Entities, "SCORE 0000000"));
        AddAssert(
            "combo HUD exists",
            () => VisualAssert.HasText(Context.Entities, "COMBO 0"));
        AddAssert(
            "PAUSE button is animated",
            () => VisualAssert.HasAnimatedText(Context.Entities, "PAUSE"));
        AddAssert(
            "play HUD layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.PlayHudLayer) == 0);
        AddAssert(
            "player exists",
            () => Context.Entities.Query<Player>().Count == 1);
        AddAssert(
            "judgement counter exists",
            () => VisualAssert.HasText(Context.Entities, "JUDGEMENTS") &&
                  VisualAssert.HasText(Context.Entities, "FLAWLESS+"));
        AddAssert(
            "key trail exists",
            () => Context.Entities.Query<KeyTrail>().Count == 1);
    }

    private void OpenPlay()
    {
        var document = new LevelDocument
        {
            Name = "Visual Play Test",
            Bpm = 120,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };

        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 260, Y = 620 });
        document.Notes.Add(new NoteDocument { TimeMs = 1_000, X = 620, Y = 520 });
        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 460, Y = 700 });

        Context.Scenes.StartPlay(
            new LevelEntry("visual://play", document));
    }
}

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
                  Context.Entities.Query<Dropdown>().Count == 2);
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
            "sidebar expands when open",
            () => SidebarHeight() > _closedSidebarHeight + 50f);
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
            () => Context.Entities.Query<EditorTimeline>().ToEntityList()[0]
                .GetComponent<EditorTimeline>().Zoom >= 32f);
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

/// <summary>Regression: Retry must rebuild the level from results and pause.</summary>
public sealed class RestartSceneTest : SceneTest
{
    public override string Name => "Retry";
    public override string Slug => "restart";

    protected override void SetUpSteps()
    {
        AddStep("open retry play scene", OpenPlay);
        AddUntilStep(
            "wait for play",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<PlaySession>().Count == 1);

        AddStep("finish play", FinishPlay);
        AddUntilStep(
            "wait for results",
            () => Context.Scenes.Current == SceneKind.Results &&
                  VisualAssert.HasText(Context.Entities, "RETRY"));

        AddStep("click results retry", ClickRetry);
        AddWaitStep("hold results retry", 2);
        AddStep("release results retry", () => Context.Pointer.Release());
        AddUntilStep(
            "restart from results",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<Note>().Count == 2 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Finished == false);
        AddAssert(
            "restarted score is reset",
            () => Context.Entities.Query<ScoreState>().ToEntityList()[0]
                .GetComponent<ScoreState>().ScorePoints == 0);

        AddStep("open pause", () => Context.Scenes.TryBack());
        AddUntilStep(
            "wait for pause retry",
            () => Context.Scenes.Current == SceneKind.Play &&
                  PauseVisible());

        AddStep("click pause retry", ClickRetry);
        AddWaitStep("hold pause retry", 2);
        AddStep("release pause retry", () => Context.Pointer.Release());
        AddUntilStep(
            "restart from pause",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<Note>().Count == 2 &&
                  !PauseVisible());
    }

    private void OpenPlay()
    {
        var document = new LevelDocument
        {
            Name = "Retry Test",
            Bpm = 120,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };

        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        document.Notes.Add(new NoteDocument { TimeMs = 1_000, X = 600, Y = 500 });

        Context.Scenes.StartPlay(new LevelEntry("memory://retry", document));
    }

    private void FinishPlay()
    {
        Entity playfield = Context.Entities.Query<PlaySession>().ToEntityList()[0];
        ref PlaySession session = ref playfield.GetComponent<PlaySession>();
        session.Finished = true;
    }

    private void ClickRetry()
    {
        foreach (Entity entity in Context.Entities
                     .Query<RetryAction, Position>()
                     .ToEntityList())
        {
            Vector2 world = WorldPosition(
                entity,
                entity.GetComponent<Position>().Value);

            if (world.X < -1_000)
                continue;

            Context.Pointer.Click(world);
            return;
        }
    }

    private bool PauseVisible()
    {
        foreach (Entity entity in Context.Entities.Query<ScenePause>().ToEntityList())
            return entity.GetComponent<ScenePause>().Visible;

        return false;
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
                  VisualAssert.HasText(Context.Entities, "MENU"));
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
            "return to main",
            () => Context.Scenes.Current == SceneKind.Main);
        AddAssert(
            "main menu is visible",
            () => VisualAssert.HasText(Context.Entities, "WRITER"));
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
                save.GetComponent<Writer.Ecs.Position>().Value);
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

/// <summary>The results screen shows the three final values in one column.</summary>
/// <summary>Regression: a movement note alone must produce a judgement.</summary>
public sealed class MovementJudgementSceneTest : SceneTest
{
    public override string Name => "Movement judgement";
    public override string Slug => "movement-judgement";

    protected override void SetUpSteps()
    {
        AddStep("start movement-only chart", () =>
        {
            var document = new LevelDocument
            {
                Name = "Movement Judgement Test",
                Bpm = 120,
                AudioPath = "res://Audio/test-track.wav",
                PlayfieldWidth = 1000,
                PlayfieldHeight = 1000,
            };
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000,
                X = 500,
                Y = 1000,
                Target = KeybindAction.L3,
                Kind = NoteKind.Movement,
                DashDirection = DashDirection.Right,
            });
            Context.Scenes.StartPlay(
                new LevelEntry("memory://movement-judgement", document));
        });
        AddUntilStep(
            "wait for movement play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddUntilStep(
            "wait for movement time",
            () => PlayheadTime() >= 900);
        AddStep("align movement note", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>()
                .ToEntityList()[0];
            Entity playfield = Context.Entities.Query<PlayfieldView>()
                .ToEntityList()[0];
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            Vector2 position = view.ScreenToPlayfield(
                player.GetComponent<Position>().Value);
            foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
            {
                ref Note value = ref note.GetComponent<Note>();
                value.Position = position;
            }
        });
        AddStep("hit movement note", () =>
        {
            Context.Keyboard.Press(
                Context.Systems.Keybinds.Get(KeybindAction.L3));
        });
        AddUntilStep(
            "movement note consumed",
            () => Context.Entities.Query<Note>().Count == 0);
        AddUntilStep(
            "movement judgement shown",
            () => Context.Entities.Query<ScoreState>().Count > 0 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().NotesHit == 1 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().JudgementText
                      .GetComponent<Text>().Value != "");
    }

    private void SetPlayheadTime(long timeMs)
    {
        foreach (Entity entity in Context.Entities
                     .Query<Playhead>()
                     .ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.TimeMs = timeMs;
            playhead.SeekRequestMs = -1;
        }
    }

    private long PlayheadTime() =>
        Context.Entities.Query<Playhead>().Count == 0
            ? 0
            : Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().TimeMs;
}

/// <summary>Live character play: hit a note, then dash on a movement note.</summary>
public sealed class CharacterPlaySceneTest : SceneTest
{
    private float _preDashX;

    public override string Name => "Character play";
    public override string Slug => "character-play";

    protected override void SetUpSteps()
    {
        AddStep("start character chart", () =>
        {
            var document = new LevelDocument
            {
                Name = "Character Play Test",
                Bpm = 120,
                AudioPath = "res://Audio/test-track.wav",
                PlayfieldWidth = 1000,
                PlayfieldHeight = 1000,
            };
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000,
                X = 500,
                Y = 1000,
                Target = KeybindAction.M,
            });
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 2_000,
                X = 500,
                Y = 1000,
                Target = KeybindAction.L3,
                Kind = NoteKind.Movement,
                DashDirection = DashDirection.Right,
            });
            Context.Scenes.StartPlay(
                new LevelEntry("memory://character-play", document));
        });
        AddUntilStep(
            "wait for character play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddUntilStep(
            "wait for first note",
            () => PlayheadTime() >= 900);
        AddStep("hit first note", () =>
        {
            Context.Keyboard.Press(
                Context.Systems.Keybinds.Get(KeybindAction.M));
        });
        AddUntilStep(
            "first note consumed",
            () => Context.Entities.Query<Note>().Count == 1);
        AddStep("release first note", () =>
        {
            Context.Keyboard.Release(
                Context.Systems.Keybinds.Get(KeybindAction.M));
        });
        AddStep("seek movement note", () => SetPlayheadTime(2_000));
        AddStep("align movement note to character", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>()
                .ToEntityList()[0];
            Entity playfield = Context.Entities.Query<PlayfieldView>()
                .ToEntityList()[0];
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            _preDashX = player.GetComponent<Position>().Value.X;
            Vector2 playfieldPosition = view.ScreenToPlayfield(
                player.GetComponent<Position>().Value);
            foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
            {
                ref Note value = ref note.GetComponent<Note>();
                value.Position = playfieldPosition;
            }
        });
        AddStep("hit movement note", () =>
        {
            Context.Keyboard.Press(
                Context.Systems.Keybinds.Get(KeybindAction.L3));
        });
        AddUntilStep(
            "movement note consumed",
            () => Context.Entities.Query<Note>().Count == 0);
        AddStep("release movement note", () =>
        {
            Context.Keyboard.Release(
                Context.Systems.Keybinds.Get(KeybindAction.L3));
        });
        AddUntilStep(
            "movement note scored",
            () => Context.Entities.Query<ScoreState>().Count > 0 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().NotesHit == 2);
        AddUntilStep(
            "movement note showed a judgement",
            () => Context.Entities.Query<ScoreState>().Count > 0 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().JudgementText
                      .GetComponent<Text>().Value != "" &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().HasJudgement);
    }

    private void SetPlayheadTime(long timeMs)
    {
        foreach (Entity entity in Context.Entities
                     .Query<Playhead>()
                     .ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.TimeMs = timeMs;
            playhead.SeekRequestMs = -1;
        }
    }

    private long PlayheadTime() =>
        Context.Entities.Query<Playhead>().Count == 0
            ? 0
            : Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().TimeMs;
}

public sealed class ResultsSceneTest : SceneTest
{
    public override string Name => "Results";
    public override string Slug => "results";

    protected override void SetUpSteps()
    {
        AddStep(
            "open results",
            () => Context.Scenes.StartResults(
                new PlayResults(
                    "Results Test",
                    987_654,
                    0.9876,
                    42,
                    30,
                    42)));

        AddUntilStep(
            "wait for results ECS",
            () => Context.Scenes.Current == SceneKind.Results &&
                  VisualAssert.HasText(Context.Entities, "RESULTS") &&
                  VisualAssert.HasText(Context.Entities, "98.76%"));

        AddAssert(
            "accuracy is shown",
            () => VisualAssert.HasText(Context.Entities, "98.76%"));
        AddAssert(
            "rank is shown",
            () => VisualAssert.HasText(Context.Entities, "S"));
        AddAssert(
            "notes hit ratio is shown",
            () => VisualAssert.HasText(Context.Entities, "30 / 42"));
        AddAssert(
            "max combo is shown",
            () => VisualAssert.HasText(Context.Entities, "42"));
        AddAssert(
            "score is shown",
            () => VisualAssert.HasText(Context.Entities, "0987654"));
        AddAssert(
            "level name is shown",
            () => VisualAssert.HasText(Context.Entities, "Results Test"));
        AddAssert(
            "results has retry and exit buttons",
            () => VisualAssert.HasText(Context.Entities, "RETRY") &&
                  VisualAssert.HasText(Context.Entities, "SONG SELECT") &&
                  VisualAssert.HasText(Context.Entities, "MAIN MENU"));
    }
}
