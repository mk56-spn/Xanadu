using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>
/// The SHORTCUTS sheet (F1, ?, or KEYS in the header): every key the animator
/// knows, in columns, from <see cref="AnimatorShortcutSheet"/>.
/// </summary>
public static partial class AnimatorLayout
{
    private const float SheetPadding = 16f;
    private const float SheetLine = 17f;
    private const float SheetKeyWidth = 120f;
    private const float SheetTextWidth = 330f;
    private const float SheetColumnGap = 18f;
    private const int SheetColumns = 3;

    /// <summary>The sheet's size, from how many rows it holds.</summary>
    public static Vector2 ShortcutsPanelSize { get; private set; }

    private static void BuildShortcutsPanel(UiBuilder menu, Vector2 screen, ref Refs refs)
    {
        Entity backdrop = menu.RectEntity(Vector2.Zero, screen,
            Palette.WithAlpha(Palette.Stage, 0.6f), takesInput: true);
        UiLayout.SetHidden(backdrop, true);
        backdrop.AddTag<UiOverlay>();
        refs.Ui.ShortcutsBackdrop = backdrop;

        var rows = AnimatorShortcutSheet.Rows();
        int perColumn = (rows.Count + SheetColumns - 1) / SheetColumns;
        float column = SheetKeyWidth + SheetTextWidth;
        float width = SheetColumns * column + (SheetColumns - 1) * SheetColumnGap;
        ShortcutsPanelSize = new Vector2(width + SheetPadding * 2, perColumn * SheetLine + 24 + 8 + SheetPadding * 2);

        menu.Container(Vector2.Zero, ShortcutsPanelSize, StackDirection.Vertical,
            spacing: 8, color: UiTheme.Panel, padding: SheetPadding, takesInput: true,
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.ShortcutsPanel = menu.Current;
        refs.Ui.ShortcutsPanel.AddTag<UiOverlayItem>();
        menu.Text("SHORTCUTS  ·  F1 OR ? TO CLOSE", new Vector2(width, 24), UiTheme.FontSize,
            UiTheme.Accent, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(width, perColumn * SheetLine), spacing: SheetColumnGap,
            color: Clear, cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        for (int c = 0; c < SheetColumns; c++)
        {
            menu.Container(Vector2.Zero, new Vector2(column, perColumn * SheetLine), StackDirection.Vertical,
                spacing: 0, color: Clear, cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
            for (int r = c * perColumn; r < rows.Count && r < (c + 1) * perColumn; r++)
            {
                AnimatorShortcutSheet.Row row = rows[r];
                menu.Container(Vector2.Zero, new Vector2(column, SheetLine), color: Clear,
                    main: StackMainAlignment.Start);
                if (row.Heading)
                {
                    menu.Text(row.Text, new Vector2(column, SheetLine), UiTheme.SmallFontSize,
                        UiTheme.Accent, TextAlign.Left);
                }
                else
                {
                    menu.Text(row.Keys, new Vector2(SheetKeyWidth, SheetLine), UiTheme.SmallFontSize,
                        UiTheme.Text, TextAlign.Left);
                    menu.Text(row.Text, new Vector2(SheetTextWidth, SheetLine), UiTheme.SmallFontSize,
                        UiTheme.TextDim, TextAlign.Left);
                }
                menu.End();
            }
            menu.End();
        }
        menu.End();
        menu.End();
    }
}
