using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Scenes;

/// <summary>Background scene for the native offset calibration wizard.</summary>
public static class OffsetWizardScene
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
            new Vector2(screen.X / 2, 90),
            new Vector2(900, 58),
            "OFFSET WIZARD",
            38,
            SceneUi.AccentColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 140),
            new Vector2(900, 28),
            "Tap SPACE with every click.",
            18,
            SceneUi.DimTextColor);

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
