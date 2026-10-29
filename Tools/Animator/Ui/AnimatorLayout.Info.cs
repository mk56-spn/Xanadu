using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The FILE INFO popup, opened from CONFIG: the file's path, which is rarely
/// changed and so has no place in the header, then what the file holds.
/// </summary>
public static partial class AnimatorLayout
{
    public static readonly Vector2 InfoPanelSize = new(460f, 262f);

    /// <summary>How many readout lines the info system fills, one fact each.</summary>
    public const int InfoLines = 6;

    private const float InfoPadding = 12f;
    private const float InfoSpacing = 8f;
    private const float InfoRowHeight = 30f;
    private const float InfoLineHeight = 18f;
    private const float InfoLabelWidth = 56f;
    private const float InfoFactWidth = 110f;

    /// <summary>
    /// The popup and its dimmer start parked; <see cref="Systems.AnimatorInfoPanelSystem"/>
    /// centres them while open. The path field is the same one the file
    /// commands read, and it applies when it loses focus.
    /// </summary>
    private static void BuildInfoPanel(UiBuilder menu, Vector2 screen, ref Refs refs)
    {
        Entity backdrop = menu.RectEntity(
            UiLayout.Hidden,
            screen,
            Palette.WithAlpha(Palette.Stage, 0.6f),
            takesInput: true);
        backdrop.AddTag<UiOverlay>();
        refs.Ui.InfoBackdrop = backdrop;

        float inner = InfoPanelSize.X - InfoPadding * 2;
        menu.Container(UiLayout.Hidden, InfoPanelSize, StackDirection.Vertical,
            spacing: InfoSpacing, color: UiTheme.Panel, padding: InfoPadding,
            takesInput: true, cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        refs.Ui.InfoPanel = menu.Current;
        refs.Ui.InfoPanel.AddTag<UiOverlayItem>();
        menu.Text("FILE INFO", new Vector2(inner, 20), UiTheme.FontSize,
            UiTheme.Accent, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(inner, InfoRowHeight),
            spacing: InfoSpacing, color: Clear, cross: StackCrossAlignment.Center);
        menu.Text("PATH", new Vector2(InfoLabelWidth, 20), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);
        refs.Ui.PathField = TextInput(menu, new Vector2(inner - InfoLabelWidth - InfoSpacing, InfoRowHeight));
        refs.Ui.PathField.AddComponent(new UiTooltip("Where SAVE writes; type a new path to save elsewhere"));
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, 4), StackDirection.Vertical,
            spacing: 4, color: Clear, cross: StackCrossAlignment.Start, autoHeight: true);
        refs.Ui.InfoText = menu.Current;
        // A label column and a value column: the font is proportional, so
        // spaces cannot line the values up.
        for (int i = 0; i < InfoLines; i++)
        {
            menu.Container(Vector2.Zero, new Vector2(inner, InfoLineHeight), color: Clear,
                main: StackMainAlignment.Start);
            menu.Text("", new Vector2(InfoFactWidth, InfoLineHeight), UiTheme.SmallFontSize,
                UiTheme.TextDim, TextAlign.Left);
            menu.Text("", new Vector2(inner - InfoFactWidth, InfoLineHeight), UiTheme.SmallFontSize,
                UiTheme.Text, TextAlign.Left);
            menu.End();
        }
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, InfoRowHeight), color: Clear,
            main: StackMainAlignment.End);
        Action(menu, "CLOSE", ToolActionKind.CloseInfo, new Vector2(120f, InfoRowHeight),
            UiTheme.SmallFontSize);
        menu.End();
        menu.End();
    }
}
