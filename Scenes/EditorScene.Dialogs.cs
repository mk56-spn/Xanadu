using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The editor's modal dialogs, built hidden with the scene: the song picker, the
/// offer to recover unsaved edits, the F10 shader slots and the map colour picker.
/// <c>EditorDialogSystem</c> opens, fills and runs them.
/// </summary>
public static partial class EditorScene
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
        IVisualRidFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.EditorDialogs, visuals, UiStage.Full);
        var dialogs = new EditorDialogs();
        BuildSongDialog(ui, screen, ref dialogs);
        BuildRecoveryDialog(ui, screen, ref dialogs);
        BuildShaderDialog(ui, screen, ref dialogs);
        BuildColorDialog(ui, screen, ref dialogs);
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
            .With(new EditorDialogButton(action));

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
        DialogButton(row, "CANCEL", 140, EditorDialogAction.Close, muted: true);
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

    private static void BuildShaderDialog(UiNode ui, Vector2 screen, ref EditorDialogs dialogs)
    {
        const float width = 620;
        float inner = width - DialogPad * 2;
        UiNode panel = Dialog(ui, screen, EditorDialogKind.Shaders, width, 540, "LEVEL SHADERS");
        panel.Text(
                $"Up to {ShaderLimits.Count} canvas shaders per level. Pick a slot, then:",
                UiType.Small,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(inner, 20);
        dialogs.ShaderList = DialogList(panel, inner, 230);
        dialogs.ShaderInfo = panel.Text("", UiType.Small, SceneUi.DimTextColor, TextAlign.Center)
            .Size(inner, 22)
            .Entity;

        const float wide = 186;
        UiNode slot = panel.Row().Gap(DialogGap);
        DialogButton(slot, "IMPORT .GDSHADER…", wide, EditorDialogAction.ImportShader);
        DialogButton(slot, "REMOVE", wide, EditorDialogAction.RemoveShader, muted: true);

        UiNode items = panel.Row().Gap(DialogGap);
        DialogButton(items, "APPLY TO SELECTION", wide, EditorDialogAction.ApplyShader);
        DialogButton(items, "CLEAR SELECTION", wide, EditorDialogAction.ClearShader, muted: true);

        const float narrow = 132;
        UiNode special = panel.Row().Gap(6);
        DialogButton(special, "USE ON PLAYER", narrow, EditorDialogAction.PlayerShader);
        DialogButton(special, "USE ON FIELD", narrow, EditorDialogAction.FieldShader);
        DialogButton(special, "PLAYER OFF", narrow, EditorDialogAction.PlayerShaderOff, muted: true);
        DialogButton(special, "FIELD OFF", narrow, EditorDialogAction.FieldShaderOff, muted: true);

        DialogButton(panel, "CLOSE", 140, EditorDialogAction.Close, muted: true);
    }

    private static void BuildColorDialog(UiNode ui, Vector2 screen, ref EditorDialogs dialogs)
    {
        const float width = 460;
        float inner = width - DialogPad * 2;
        UiNode panel = Dialog(ui, screen, EditorDialogKind.MapColor, width, 360, "", out dialogs.ColorTitle);
        dialogs.ColorHint = panel.Text("", UiType.Small, SceneUi.DimTextColor, TextAlign.Center)
            .Size(inner, 22)
            .Entity;

        // The picker places its own parts round its square, so it gets a slot of its own.
        const float slotHeight = ColorPickerLayout.Height + ColorPickerLayout.AlphaHeight + 16;
        UiNode slot = panel.Column().Size(inner, slotHeight);
        var square = new Vector2(
            -inner / 2 + ColorPickerLayout.Size.X / 2 + 20,
            -slotHeight / 2 + ColorPickerLayout.Size.Y / 2 + 4);
        dialogs.ColorPicker = slot.ColorPicker(square, Colors.White).Entity;

        DialogButton(panel, "DONE", 140, EditorDialogAction.Close);
    }
}
