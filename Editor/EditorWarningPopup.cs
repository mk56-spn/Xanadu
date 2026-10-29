using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Editor;

/// <summary>Builds and requests the editor's ECS warning popup.</summary>
public static class EditorWarningPopup
{
    public const int LayerZIndex = 850;
    public const string NoSongMessage = "Load a song before placing notes.";


    private static readonly Vector2 PopupSize = new(440, 170);

    /// <summary>Creates the popup hierarchy hidden below the editor overlay.</summary>
    public static Entity Build(EntityStore entities, IVisualRidFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, default, LayerZIndex, visuals, UiStage.Full);

        UiNode panel = ui.Column()
            .Size(PopupSize)
            .At(UiLayout.Hidden)
            .Gap(10)
            .Fill(Palette.DangerSurfaceAlt);

        panel.Rect(Palette.DangerDark, "WARNING", new Vector2(400, 26));
        Entity message = panel.Rect(
                Palette.DangerSurfaceAlt,
                NoSongMessage,
                new Vector2(400, 48))
            .Entity;
        Entity button = panel.Rect(
                Palette.WarningDeep,
                "OK",
                new Vector2(120, 34))
            .Input()
            .With(ButtonVisual.Create(Palette.WarningDeep))
            .Entity;

        panel.With(new WarningPopup(message, button));
        return panel.Entity;
    }

    /// <summary>Queues a warning for the popup system to display.</summary>
    public static void Request(Entity editor, string message)
    {
        if (editor.IsNull)
            return;

        if (editor.HasComponent<WarningRequest>())
        {
            ref WarningRequest request = ref editor.GetComponent<WarningRequest>();
            request.Message = message;
            return;
        }

        editor.AddComponent(new WarningRequest(message));
    }

}
