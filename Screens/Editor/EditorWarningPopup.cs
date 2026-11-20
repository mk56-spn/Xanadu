using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Logic.Editor;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>Builds and requests the editor's ECS warning popup.</summary>
public static class EditorWarningPopup
{
    private static readonly Vector2 PopupSize = new(440, 170);

    /// <summary>Creates the popup hierarchy hidden below the editor overlay.</summary>
    public static Entity Build(EntityStore entities, IVisualItemFactory? visuals = null)
    {
        var ui = UiNode.Layer(entities, default, UiLayers.EditorWarning, visuals, UiStage.Full);

        UiNode panel = ui.Column().Size(PopupSize).Hidden().Gap(10).Fill(Palette.DangerSurfaceAlt);

        panel.Rect(Palette.DangerDark, "WARNING", new Vector2(400, 26));
        Entity message = panel.Rect(Palette.DangerSurfaceAlt, EditorWarnings.NoSongMessage, new Vector2(400, 48)).Entity;
        Entity button = panel.Rect(Palette.WarningDeep, "OK", new Vector2(120, 34)).Input()
            .With(ButtonVisual.Create(Palette.WarningDeep)).Entity;

        panel.With(new WarningPopup(message, button));
        return panel.Entity;
    }
}
