using System.Collections.Generic;
using Friflo.Engine.ECS;
using Writer.Ecs;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;

namespace Writer.VisualTests;

/// <summary>Structural regression cases for the ECS screens.</summary>
public static class VisualTestCatalog
{
    public static IReadOnlyList<IVisualTest> All { get; } = new IVisualTest[]
    {
        new MainMenuVisualTest(),
        new SongSelectVisualTest(),
        new SettingsVisualTest(),
        new PlayVisualTest(),
    };
}

public sealed class MainMenuVisualTest : IVisualTest
{
    public string Name => "Main menu";
    public string Slug => "main";
    public SceneKind Scene => SceneKind.Main;

    public void Begin(VisualTestContext context) =>
        context.Scenes.Start(Scene);

    public bool IsReady(VisualTestContext context) =>
        VisualAssert.IsSceneReady(context, Scene, SceneUi.MenuLayer) &&
        VisualAssert.HasText(context.Entities, "WRITER");

    public VisualTestResult Verify(VisualTestContext context)
    {
        if (!VisualAssert.HasText(context.Entities, "WRITER"))
            return VisualTestResult.Fail("missing WRITER title");

        if (!VisualAssert.HasText(context.Entities, "PLAY"))
            return VisualTestResult.Fail("missing PLAY button");

        int uninitialised = VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.MenuLayer);

        return uninitialised > 0
            ? VisualTestResult.Fail(
                $"{uninitialised} menu drawables are still uninitialised")
            : VisualTestResult.Pass("menu layer initialised");
    }
}

public sealed class SongSelectVisualTest : IVisualTest
{
    public string Name => "Song select";
    public string Slug => "song-select";
    public SceneKind Scene => SceneKind.SongSelect;

    public void Begin(VisualTestContext context) =>
        context.Scenes.Start(Scene);

    public bool IsReady(VisualTestContext context) =>
        VisualAssert.IsSceneReady(context, Scene, SceneUi.MenuLayer) &&
        VisualAssert.HasText(context.Entities, "SONG SELECT");

    public VisualTestResult Verify(VisualTestContext context)
    {
        if (!VisualAssert.HasText(context.Entities, "SONG SELECT"))
            return VisualTestResult.Fail("missing SONG SELECT title");

        int uninitialised = VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.MenuLayer);

        return uninitialised > 0
            ? VisualTestResult.Fail(
                $"{uninitialised} song select drawables are still uninitialised")
            : VisualTestResult.Pass("song select layer initialised");
    }
}

public sealed class SettingsVisualTest : IVisualTest
{
    public string Name => "Settings keybinds";
    public string Slug => "settings";
    public SceneKind Scene => SceneKind.Settings;

    public void Begin(VisualTestContext context) =>
        context.Scenes.Start(Scene);

    public bool IsReady(VisualTestContext context) =>
        context.Scenes.Current == Scene &&
        VisualAssert.HasLayer(context.Entities, SceneUi.MenuLayer - 1) &&
        VisualAssert.HasLayer(context.Entities, SceneUi.MenuLayer) &&
        VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.MenuLayer - 1) == 0 &&
        VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.MenuLayer) == 0 &&
        VisualAssert.HasText(context.Entities, "L1");

    public VisualTestResult Verify(VisualTestContext context)
    {
        if (!VisualAssert.HasText(context.Entities, "SETTINGS"))
            return VisualTestResult.Fail("missing SETTINGS title");

        if (!VisualAssert.HasText(context.Entities, "KEYBINDS"))
            return VisualTestResult.Fail("missing KEYBINDS heading");

        if (!VisualAssert.HasText(context.Entities, "L1") ||
            !VisualAssert.HasText(context.Entities, "SPACE"))
        {
            return VisualTestResult.Fail("missing keybind rows");
        }

        Entity backgroundLayer =
            VisualAssert.LayerAt(context.Entities, SceneUi.MenuLayer - 1);
        Entity contentLayer =
            VisualAssert.LayerAt(context.Entities, SceneUi.MenuLayer);

        if (backgroundLayer.IsNull || contentLayer.IsNull)
            return VisualTestResult.Fail("background/content layers are missing");

        if (contentLayer.GetComponent<Layer>().ZIndex <=
            backgroundLayer.GetComponent<Layer>().ZIndex)
        {
            return VisualTestResult.Fail(
                "content layer is not above the background layer");
        }

        var buttons = context.Entities
            .Query<KeybindButton>()
            .ToEntityList();

        if (buttons.Count != KeybindSettings.Actions.Length)
        {
            return VisualTestResult.Fail(
                $"expected {KeybindSettings.Actions.Length} keybind buttons, found {buttons.Count}");
        }

        foreach (Entity button in buttons)
        {
            if (button.GetComponent<Drawable>().ZIndex != SceneUi.MenuLayer)
            {
                return VisualTestResult.Fail(
                    $"keybind {button.GetComponent<KeybindButton>().Action} is not on the content layer");
            }

            if (button.Parent.IsNull ||
                button.Parent.Id != contentLayer.Id)
            {
                return VisualTestResult.Fail(
                    $"keybind {button.GetComponent<KeybindButton>().Action} is not parented above the background");
            }

            if (button.GetComponent<Rectangle>().Bounds.Size.X <= 0 ||
                button.GetComponent<Rectangle>().Bounds.Size.Y <= 0)
            {
                return VisualTestResult.Fail(
                    $"keybind {button.GetComponent<KeybindButton>().Action} has an empty rectangle");
            }
        }

        int uninitialised = VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.MenuLayer);

        return uninitialised > 0
            ? VisualTestResult.Fail(
                $"{uninitialised} settings drawables are still uninitialised")
            : VisualTestResult.Pass("keybinds are on the content layer");
    }
}

public sealed class PlayVisualTest : IVisualTest
{
    public string Name => "Play HUD and field";
    public string Slug => "play";
    public SceneKind Scene => SceneKind.Play;

    public void Begin(VisualTestContext context)
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

        context.Scenes.StartPlay(
            new LevelEntry("visual://play", document));
    }

    public bool IsReady(VisualTestContext context) =>
        VisualAssert.IsSceneReady(context, Scene, SceneUi.PlayHudLayer) &&
        VisualAssert.HasText(context.Entities, "Visual Play Test");

    public VisualTestResult Verify(VisualTestContext context)
    {
        if (!VisualAssert.HasText(context.Entities, "Visual Play Test"))
            return VisualTestResult.Fail("missing level title");

        if (!VisualAssert.HasText(context.Entities, "SCORE 0000000") ||
            !VisualAssert.HasText(context.Entities, "COMBO 0"))
        {
            return VisualTestResult.Fail("missing score/combo HUD");
        }

        int uninitialised = VisualAssert.CountUninitialised(
            context.Entities,
            SceneUi.PlayHudLayer);

        return uninitialised > 0
            ? VisualTestResult.Fail(
                $"{uninitialised} play HUD drawables are still uninitialised")
            : VisualTestResult.Pass("play HUD initialised");
    }
}
