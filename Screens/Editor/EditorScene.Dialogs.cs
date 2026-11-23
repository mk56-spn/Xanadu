using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The editor's modal dialogs, built hidden with the scene: the song picker, the
/// offer to recover unsaved edits, the F10 shader slots and the map colour picker.
/// <c>EditorDialogSystem</c> opens, fills and runs them.
/// </summary>
public static class EditorSceneDialogs
{
    public const float DialogListRowHeight = 30;
    private const float DialogPad = 16;
    private const float DialogGap = 10;
    private const float DialogButtonHeight = 32;
    private static readonly Color DialogScrim = new(0f, 0f, 0f, 0.6f);

    /// <summary>Builds the dialogs hidden under <paramref name="root"/> and gives the editor their <see cref="EditorDialogs"/>.</summary>
    public static void BuildDialogs(
        EntityStore entities,
        Entity root,
        Entity editor,
        Vector2 screen,
        IVisualItemFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.EditorDialogs, visuals, UiStage.Full);
        var dialogs = new EditorDialogs();
        BuildSongDialog(ui, screen, ref dialogs);
        BuildRecoveryDialog(ui, screen, ref dialogs);
        editor.AddComponent(dialogs);
    }

    /// <summary>The full-screen scrim that makes a dialog modal, and the panel in its middle.</summary>
    private static UiNode Dialog(UiNode ui, Vector2 screen, EditorDialogKind kind, float width, float height, string title) =>
        Dialog(ui, screen, kind, width, height, title, out _);

    private static UiNode Dialog(
        UiNode ui,
        Vector2 screen,
        EditorDialogKind kind,
        float width,
        float height,
        string title,
        out Entity heading)
    {
        UiNode scrim = ui.Column()
            .Cover(screen)
            .Fill(DialogScrim)
            .Input()
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center)
            .Hidden()
            .With(new EditorDialog(kind));
        UiNode panel = scrim.Column()
            .Size(width, height)
            .Fill(SceneUi.PanelColor)
            .Input()
            .Pad(DialogPad)
            .Gap(DialogGap)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Center);
        heading = panel.Text(title, UiType.Section, SceneUi.AccentColor, TextAlign.Center)
            .Size(width - DialogPad * 2, 28)
            .Entity;
        return panel;
    }

    private static UiNode DialogButton(UiNode row, string label, float width, EditorDialogAction action, bool muted = false) =>
        row.Button(label, 13, muted ? SceneUi.MutedButtonColor : SceneUi.EditorButtonColor)
            .Size(width, DialogButtonHeight)
            .With(UiCommand.Of(action));

    /// <summary>A scrolled list whose rows the dialog system adds as it fills it.</summary>
    private static Entity DialogList(UiNode panel, float width, float height)
    {
        panel.Scroll(new Vector2(width, height), 0, out UiNode content)
            .Fill(Palette.SurfaceInset);
        content.IgnoreLayout()
            .AutoHeight()
            .Gap(2)
            .Main(StackMainAlignment.Start)
            .Cross(StackCrossAlignment.Start);
        return content.Entity;
    }

    private static void BuildSongDialog(UiNode ui, Vector2 screen, ref EditorDialogs dialogs)
    {
        const float width = 660;
        UiNode panel = Dialog(ui, screen, EditorDialogKind.Song, width, 520, "CHOOSE A SONG");
        dialogs.SongList = DialogList(panel, width - DialogPad * 2, 340);
        dialogs.SongInfo = panel.Text("", UiType.Small, SceneUi.DimTextColor, TextAlign.Center)
            .Size(width - DialogPad * 2, 22)
            .Entity;

        UiNode row = panel.Row().Gap(DialogGap);
        dialogs.SongUse = DialogButton(row, "SELECT", 140, EditorDialogAction.UseSong).Entity;
        dialogs.SongCancel = DialogButton(row, "CANCEL", 140, EditorDialogAction.Close, muted: true).Entity;
        dialogs.SongBack = DialogButton(row, "BACK", 140, EditorDialogAction.Leave, muted: true)
            .Hidden()
            .Entity;
    }

    private static void BuildRecoveryDialog(UiNode ui, Vector2 screen, ref EditorDialogs dialogs)
    {
        const float width = 540;
        UiNode panel = Dialog(ui, screen, EditorDialogKind.Recovery, width, 200, "RECOVER UNSAVED CHANGES");
        dialogs.RecoveryMessage = panel.Text("", UiType.Body, SceneUi.TextColor, TextAlign.Center)
            .Size(width - DialogPad * 2, 64)
            .Entity;

        UiNode row = panel.Row().Gap(DialogGap);
        DialogButton(row, "RECOVER", 150, EditorDialogAction.Recover);
        DialogButton(row, "DISCARD", 150, EditorDialogAction.Discard, muted: true);
    }

}
