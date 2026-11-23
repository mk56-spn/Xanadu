using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>Builds and requests the editor's ECS warning popup.</summary>
public static class EditorWarningPopup
{
    private static readonly Vector2 PopupSize = new(440, 170);

    /// <summary>
    /// Creates the popup hierarchy hidden below the editor overlay; a
    /// <see cref="WarningRaised"/> on <paramref name="editor"/> shows it.
    /// </summary>
    public static Entity Build(EntityStore entities, Entity editor, IVisualItemFactory? visuals = null)
    {
        var ui = UiNode.Layer(entities, default, UiLayers.EditorWarning, visuals, UiStage.Full);

        UiNode panel = ui.Column().Size(PopupSize).Hidden().Gap(10).Fill(Palette.DangerSurfaceAlt);

        panel.Rect(Palette.DangerDark, "WARNING", new Vector2(400, 26));
        Entity message = panel.Rect(Palette.DangerSurfaceAlt, EditorWarnings.NoSongMessage, new Vector2(400, 48)).Entity;
        Entity button = panel.Rect(Palette.WarningDeep, "OK", new Vector2(120, 34)).Input()
            .With(ButtonVisual.Create(Palette.WarningDeep)).Entity;

        panel.With(new WarningPopup(message, button));
        Entity popup = panel.Entity;
        editor.AddSignalHandler<WarningRaised>(signal => Show(popup, editor, signal.Event.Message));
        return popup;
    }

    /// <summary>Shows <paramref name="message"/> centred on the playfield, unless the popup is already up.</summary>
    private static void Show(Entity popup, Entity editor, string message)
    {
        if (popup.IsNull || popup.Enabled || !popup.HasComponent<WarningPopup>())
            return;

        Entity text = popup.GetComponent<WarningPopup>().Message;
        if (!text.IsNull && text.HasComponent<Text>())
            UiText.Set(text, message);

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        popup.GetComponent<Position>().Value = view.PlayfieldToScreen(new Vector2(playfield.Width / 2f, playfield.Height / 2f));
        popup.AddTag<Moved>();
        UiLayout.SetHidden(popup, false);
    }
}
