using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Scenes;

/// <summary>Settings scene with rebindable playfield keys.</summary>
public static class SettingsScene
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        KeybindSettings keybinds,
        DisplaySettings display,
        IVisualRidFactory? visuals = null)
    {
        float centerX = screen.X / 2;
        float centerY = screen.Y / 2 + 40;
        const float panelWidth = 760;
        float panelHeight = Mathf.Min(600, Mathf.Max(500, screen.Y - 120));
        float panelTop = centerY - panelHeight / 2;
        float panelLeft = centerX - panelWidth / 2;

        // Keep the full-screen background and panel on a lower layer so they
        // cannot win sibling draw order against the content layer.
        UiBuilder background = SceneUi.Layer(
            entities,
            root,
            SceneUi.MenuLayer - 1,
            visuals);
        SceneUi.Background(background, screen);
        background.RectEntity(
            new Vector2(centerX, centerY),
            new Vector2(panelWidth, panelHeight),
            SceneUi.PanelColor);

        UiBuilder ui = SceneUi.Layer(
            entities,
            root,
            SceneUi.MenuLayer,
            visuals);

        SceneUi.Text(
            ui,
            new Vector2(centerX, 90),
            new Vector2(900, 64),
            "SETTINGS",
            40,
            SceneUi.AccentColor);

        SceneUi.Text(
            ui,
            new Vector2(panelLeft + 190, panelTop + 42),
            new Vector2(220, 34),
            "VERTICAL SYNC",
            18,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        Entity vsync = ui.RectEntity(
            new Vector2(panelLeft + panelWidth - 170, panelTop + 42),
            new Vector2(240, 38),
            SceneUi.MutedButtonColor,
            display.VSyncEnabled ? "V-SYNC: ON" : "V-SYNC: OFF",
            takesInput: true);
        vsync.AddComponent(new TextStyle(16, SceneUi.TextColor));
        vsync.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        vsync.AddComponent(new VSyncToggle());

        SceneUi.Text(
            ui,
            new Vector2(centerX, panelTop + 88),
            new Vector2(panelWidth - 80, 30),
            "KEYBINDS",
            22,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(panelLeft + 190, panelTop + 128),
            new Vector2(220, 24),
            "ACTION",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(panelLeft + panelWidth - 170, panelTop + 128),
            new Vector2(220, 24),
            "KEY",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        int index = 0;
        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            float rowY = panelTop + 170 + index * 42;

            SceneUi.Text(
                ui,
                new Vector2(panelLeft + 190, rowY),
                new Vector2(220, 34),
                KeybindSettings.LabelFor(action),
                18,
                SceneUi.TextColor,
                SceneUi.PanelColor);

            Entity button = ui.RectEntity(
                new Vector2(panelLeft + panelWidth - 170, rowY),
                new Vector2(240, 38),
                SceneUi.MutedButtonColor,
                KeybindSettings.KeyLabel(keybinds.Get(action)),
                takesInput: true);

            button.AddComponent(new TextStyle(16, SceneUi.TextColor));
            button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
            button.AddComponent(new KeybindButton(action));
            index++;
        }

        SceneUi.Text(
            ui,
            new Vector2(centerX, panelTop + panelHeight - 28),
            new Vector2(panelWidth - 80, 24),
            "CLICK A KEY TO REBIND   ·   ESC CANCELS",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Button(
            ui,
            new Vector2(100, 44),
            new Vector2(150, 44),
            "BACK",
            SceneKind.Main,
            18,
            SceneUi.MutedButtonColor);
    }
}
