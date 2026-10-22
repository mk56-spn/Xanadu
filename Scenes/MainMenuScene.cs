using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Strings;

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
            LocalizedText.Get(MainMenuStrings.Title),
            52,
            SceneUi.AccentColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 205),
            new Vector2(900, 30),
            LocalizedText.Get(MainMenuStrings.Subtitle),
            18,
            SceneUi.DimTextColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 360),
            new Vector2(360, 66),
            LocalizedText.Get(MainMenuStrings.Play),
            SceneKind.SongSelect,
            26);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 442),
            new Vector2(360, 56),
            LocalizedText.Get(MainMenuStrings.Editor),
            SceneKind.Editor,
            20,
            SceneUi.MutedButtonColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 512),
            new Vector2(360, 56),
            LocalizedText.Get(MainMenuStrings.Settings),
            SceneKind.Settings,
            20,
            SceneUi.MutedButtonColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 580),
            new Vector2(360, 50),
            LocalizedText.Get(MainMenuStrings.OffsetWizard),
            SceneKind.OffsetWizard,
            17,
            SceneUi.MutedButtonColor);

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, 648),
            new Vector2(360, 50),
            LocalizedText.Get(MainMenuStrings.Profiles),
            SceneKind.Profiles,
            17,
            SceneUi.MutedButtonColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y - 40),
            new Vector2(900, 24),
            LocalizedText.Get(MainMenuStrings.Footer),
            14,
            SceneUi.DimTextColor);
    }
}
