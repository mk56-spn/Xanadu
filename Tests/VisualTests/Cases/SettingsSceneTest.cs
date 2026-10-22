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
