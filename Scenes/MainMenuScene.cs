using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Ui;

namespace Writer.Scenes;

/// <summary>Title screen with play, editor, and settings entry points.</summary>
public static class MainMenuScene
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
            new Vector2(screen.X / 2, 140),
            new Vector2(900, 84),
            "WRITER",
            52,
            SceneUi.AccentColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 205),
            new Vector2(900, 30),
            "RHYTHM GAME EDITOR",
            18,
            SceneUi.DimTextColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 360),
            new Vector2(360, 66),
            "PLAY",
            SceneKind.SongSelect,
            26);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 442),
            new Vector2(360, 56),
            "EDITOR",
            SceneKind.Editor,
            20,
            SceneUi.MutedButtonColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 512),
            new Vector2(360, 56),
            "SETTINGS",
            SceneKind.Settings,
            20,
            SceneUi.MutedButtonColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y - 40),
            new Vector2(900, 24),
            "PLAY  ·  EDIT  ·  CREATE",
            14,
            SceneUi.DimTextColor);
    }
}
