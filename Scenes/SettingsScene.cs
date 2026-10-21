using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Ui;

namespace Writer.Scenes;

/// <summary>Placeholder settings scene for the ECS scene flow.</summary>
public static class SettingsScene
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 130),
            new Vector2(900, 64),
            "SETTINGS",
            40,
            SceneUi.AccentColor);

        ui.RectEntity(
            new Vector2(screen.X / 2, screen.Y / 2),
            new Vector2(620, 180),
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y / 2 - 20),
            new Vector2(560, 30),
            "NOTHING TO CONFIGURE YET",
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y / 2 + 25),
            new Vector2(560, 26),
            "AUDIO, INPUT, AND DISPLAY OPTIONS WILL LIVE HERE",
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
