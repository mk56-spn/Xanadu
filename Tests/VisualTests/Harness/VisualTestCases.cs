using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;

namespace Writer.Testing;

/// <summary>Structural regression cases for the ECS screens.</summary>
public static class SceneTestCatalog
{
    public static IReadOnlyList<SceneTest> All { get; } = new SceneTest[]
    {
        new MainMenuSceneTest(),
        new SongSelectSceneTest(),
        new SettingsSceneTest(),
        new PlaySceneTest(),
        new ResultsSceneTest(),
        new LeaveEditorSceneTest(),
    };
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

/// <summary>Regression: the leave-editor confirmation must accept a real click.</summary>
public sealed class LeaveEditorSceneTest : SceneTest
{
    private EditorOverlay? _overlay;

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
        AddWaitStep("overlay settles", 2);
        AddStep("request leave", () => Context.Scenes.TryBack());
        AddUntilStep("leave confirmation is visible", ConfirmationIsVisible);
        AddStep("click YES", ClickYes);
        AddWaitStep("hold click", 2);
        AddStep("release click", () => Context.Pointer.Release());
        AddUntilStep(
            "return to main",
            () => Context.Scenes.Current == SceneKind.Main);
        AddAssert(
            "main menu is visible",
            () => VisualAssert.HasText(Context.Entities, "WRITER"));
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

    private void ClickYes()
    {
        foreach (Entity entity in Context.Entities.Query<SceneConfirmation>().ToEntityList())
        {
            SceneConfirmation confirmation = entity.GetComponent<SceneConfirmation>();
            if (!confirmation.Visible || confirmation.Yes.IsNull)
                continue;

            Entity yes = confirmation.Yes;
            Vector2 world = UiTransform.WorldPosition(
                yes,
                yes.GetComponent<Writer.Ecs.Position>().Value);
            Context.Pointer.Click(world);
            return;
        }

        throw new SceneTestAssertionException("leave confirmation YES button missing");
    }

    private void CloseOverlay()
    {
        _overlay?.Close();
        _overlay = null;
    }
}

/// <summary>The results screen shows the three final values in one column.</summary>
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
            "results has two exit buttons",
            () => VisualAssert.HasText(Context.Entities, "SONG SELECT") &&
                  VisualAssert.HasText(Context.Entities, "MAIN MENU"));
    }
}
