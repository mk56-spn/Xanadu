using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>The groups grid in the editor's sidebar and the overlay screen a group opens.</summary>
public static partial class EditorScene
{
    /// <summary>Room the grid takes in the sidebar: its label, four rows of cells and a margin.</summary>
    public const float GroupGridHeight = EditorGroupLayout.GridHeight;

    /// <summary>The groups label, a grid of the group cells and the preview toggle, flowing in the sidebar.</summary>
    private static void BuildGroupGrid(UiNode ui, Entity editor, float width)
    {
        const float cell = 40f;
        const float gap = 6f;

        Entity label = SidebarLabel(ui, "GROUPS", width, 14);

        var cells = new Entity[GroupLimits.Count];
        UiNode grid = ui.Grid(
            GroupLimits.Columns,
            gap: gap,
            rowGap: UiTheme.ContainerSpacing);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = grid.Button(
                    (i + 1).ToString(),
                    UiType.Small,
                    SceneUi.MutedButtonColor)
                .Size(cell, 34)
                .With(new EditorGroupCell(i))
                .Tooltip($"Edit group {i + 1}")
                .Entity;
        }

        Entity preview = ui.Button(
                "PREVIEW OFF",
                12,
                SceneUi.MutedButtonColor)
            .Size(150, 30)
            .Tooltip("Show where the groups move and tint their members at the playhead")
            .Entity;

        editor.AddComponent(new EditorGroupGrid(label, cells, preview));
    }

    /// <summary>
    /// The group overlay: a dimmed screen and a dialog with the group's name,
    /// its members, its effects as a table, and a timeline of them. Every piece
    /// starts off-screen; <c>EditorGroupSystem</c> places the pieces while a
    /// group is open.
    /// </summary>
    private static void BuildGroupOverlay(EntityStore entities, Entity root, Vector2 screen)
    {
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Color panel = Palette.SurfaceDeep;
        Color field = Palette.SurfaceRaised;

        UiNode scrim = UiNode.Layer(entities, root, 975, null, UiStage.Full);
        Piece(
            scrim.Rect(new Color(0, 0, 0, 0.7f))
                .Size(screen)
                .At(UiLayout.Hidden)
                .Input(),
            EditorGroupPart.Scrim,
            0,
            new Rect2(Vector2.Zero, screen));

        UiNode back = UiNode.Layer(entities, root, 976, null, UiStage.Full);
        Piece(
            back.Rect(panel).Size(dialog).At(UiLayout.Hidden).Input(),
            EditorGroupPart.Dialog,
            0,
            Place(EditorGroupPart.Dialog, 0, dialog));

        UiNode tracks = UiNode.Layer(entities, root, 977, null, UiStage.Full);
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Rect2 track = Place(EditorGroupPart.RowTrack, row, dialog);
            Piece(
                tracks.Rect(field).Size(track.Size).At(UiLayout.Hidden),
                EditorGroupPart.RowTrack,
                row,
                track);
        }

        UiNode ticks = UiNode.Layer(entities, root, 978, null, UiStage.Full);
        for (int slot = 0; slot < EditorGroupLayout.TickSlots; slot++)
        {
            Piece(
                ticks.Rect(Palette.SurfaceAlt)
                    .Size(new Vector2(
                        1,
                        EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight))
                    .At(UiLayout.Hidden),
                EditorGroupPart.TickLine,
                slot,
                new Rect2(
                    0,
                    0,
                    1,
                    EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight));
        }

        Piece(
            ticks.Rect(Colors.White)
                .Size(new Vector2(
                    2,
                    EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight))
                .At(UiLayout.Hidden),
            EditorGroupPart.PlayheadLine,
            0,
            Place(EditorGroupPart.PlayheadLine, 0, dialog));

        UiNode bars = UiNode.Layer(entities, root, 979, null, UiStage.Full);
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Rect2 track = Place(EditorGroupPart.RowTrack, row, dialog);
            Piece(
                bars.Rect(Palette.Info)
                    .Size(new Vector2(10, track.Size.Y - 8))
                    .At(UiLayout.Hidden),
                EditorGroupPart.RowBar,
                row,
                new Rect2(0, 0, 10, track.Size.Y - 8));
        }

        UiNode ui = UiNode.Layer(entities, root, 980, null, UiStage.Full);
        for (int slot = 0; slot < EditorGroupLayout.LabelSlots; slot++)
        {
            Piece(
                ui.Text("", 11, SceneUi.DimTextColor)
                    .Size(70, 16)
                    .At(UiLayout.Hidden),
                EditorGroupPart.TickLabel,
                slot,
                new Rect2(0, 0, 70, 16));
        }

        Piece(
            ui.Text("GROUP", 22, SceneUi.AccentColor, TextAlign.Left)
                .Size(190, 30)
                .At(UiLayout.Hidden),
            EditorGroupPart.Title,
            0,
            Place(EditorGroupPart.Title, 0, dialog));

        Piece(
            GroupField(ui, "", false, 280),
            EditorGroupPart.NameField,
            0,
            Place(EditorGroupPart.NameField, 0, dialog));
        Piece(
            GroupButton(ui, "DONE", SceneUi.ButtonColor, 100, 30),
            EditorGroupPart.Close,
            0,
            Place(EditorGroupPart.Close, 0, dialog));

        Piece(
            ui.Text("", 13, SceneUi.TextColor, TextAlign.Left)
                .Size(190, 28)
                .At(UiLayout.Hidden),
            EditorGroupPart.MemberText,
            0,
            Place(EditorGroupPart.MemberText, 0, dialog));
        Piece(GroupButton(ui, "ADD SELECTED", SceneUi.ButtonColor, 130, 28), EditorGroupPart.AddSelected, 0, Place(EditorGroupPart.AddSelected, 0, dialog));
        Piece(GroupButton(ui, "REMOVE SELECTED", SceneUi.MutedButtonColor, 150, 28), EditorGroupPart.RemoveSelected, 0, Place(EditorGroupPart.RemoveSelected, 0, dialog));
        Piece(GroupButton(ui, "SELECT MEMBERS", SceneUi.MutedButtonColor, 140, 28), EditorGroupPart.SelectMembers, 0, Place(EditorGroupPart.SelectMembers, 0, dialog));
        Piece(GroupButton(ui, "+ MOVE", SceneUi.ButtonColor, 100, 28), EditorGroupPart.AddMove, 0, Place(EditorGroupPart.AddMove, 0, dialog));
        Piece(GroupButton(ui, "+ COLOR", SceneUi.ButtonColor, 100, 28), EditorGroupPart.AddColor, 0, Place(EditorGroupPart.AddColor, 0, dialog));
        Piece(GroupButton(ui, "SNAP", SceneUi.ButtonColor, 110, 28), EditorGroupPart.SnapToggle, 0, Place(EditorGroupPart.SnapToggle, 0, dialog));
        Piece(
            GroupButton(ui, "SOLO", SceneUi.MutedButtonColor, 88, 28)
                .Tooltip("Preview only this group"),
            EditorGroupPart.SoloToggle,
            0,
            Place(EditorGroupPart.SoloToggle, 0, dialog));
        Piece(
            GroupButton(ui, "PLAY", SceneUi.ButtonColor, 70, 28)
                .Tooltip("Play or pause the live preview"),
            EditorGroupPart.PlayToggle,
            0,
            Place(EditorGroupPart.PlayToggle, 0, dialog));
        Piece(GroupButton(ui, "<", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollLeft, 0, Place(EditorGroupPart.ScrollLeft, 0, dialog));
        Piece(GroupButton(ui, ">", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollRight, 0, Place(EditorGroupPart.ScrollRight, 0, dialog));
        Piece(GroupButton(ui, "-", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomOut, 0, Place(EditorGroupPart.ZoomOut, 0, dialog));
        Piece(GroupButton(ui, "+", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomIn, 0, Place(EditorGroupPart.ZoomIn, 0, dialog));

        Piece(
            ui.Text("NO EFFECT", 11, SceneUi.DimTextColor, TextAlign.Left)
                .Size(170, 28)
                .At(UiLayout.Hidden),
            EditorGroupPart.SelectionText,
            0,
            Place(EditorGroupPart.SelectionText, 0, dialog));
        Piece(
            GroupButton(ui, "TARGET", SceneUi.MutedButtonColor, 70, 28)
                .Tooltip("Drag the selected move's offset on the playfield"),
            EditorGroupPart.SelectionTarget,
            0,
            Place(EditorGroupPart.SelectionTarget, 0, dialog));
        Piece(
            GroupButton(ui, "DUP", SceneUi.MutedButtonColor, 60, 28)
                .Tooltip("Duplicate the selected effect"),
            EditorGroupPart.SelectionDuplicate,
            0,
            Place(EditorGroupPart.SelectionDuplicate, 0, dialog));
        Piece(
            GroupButton(ui, "MUTE", SceneUi.MutedButtonColor, 70, 28)
                .Tooltip("Mute or unmute the selected effect"),
            EditorGroupPart.SelectionMute,
            0,
            Place(EditorGroupPart.SelectionMute, 0, dialog));
        Piece(
            GroupButton(ui, "◀", SceneUi.MutedButtonColor, 32, 28)
                .Tooltip("Nudge the selected effect one beat left"),
            EditorGroupPart.SelectionNudgeLeft,
            0,
            Place(EditorGroupPart.SelectionNudgeLeft, 0, dialog));
        Piece(
            GroupButton(ui, "▶", SceneUi.MutedButtonColor, 32, 28)
                .Tooltip("Nudge the selected effect one beat right"),
            EditorGroupPart.SelectionNudgeRight,
            0,
            Place(EditorGroupPart.SelectionNudgeRight, 0, dialog));
        Piece(
            GroupButton(ui, "PICK", SceneUi.MutedButtonColor, 54, 28)
                .Tooltip("Pick a colour for the selected colour effect"),
            EditorGroupPart.SelectionPick,
            0,
            Place(EditorGroupPart.SelectionPick, 0, dialog));

        Piece(
            ui.Text(
                    "Drag a bar to retime, its ends for START/END. EASE cycles the curve. Pick a row to use the selection bar.",
                    11,
                    SceneUi.DimTextColor,
                    TextAlign.Left)
                .Size(dialog.X - 40, 44)
                .At(UiLayout.Hidden),
            EditorGroupPart.Hint,
            0,
            Place(EditorGroupPart.Hint, 0, dialog));

        UiNode previewUi = UiNode.Layer(entities, root, 981, null, UiStage.Full);
        Rect2 previewArea = Place(EditorGroupPart.PreviewPanel, 0, dialog);
        Piece(
            previewUi.Rect(Palette.SurfaceInset)
                .Size(previewArea.Size)
                .At(UiLayout.Hidden),
            EditorGroupPart.PreviewPanel,
            0,
            previewArea);

        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Piece(
                ui.Text((row + 1).ToString(), 12, SceneUi.DimTextColor)
                    .Size(24, 28)
                    .At(UiLayout.Hidden),
                EditorGroupPart.RowIndex,
                row,
                Place(EditorGroupPart.RowIndex, row, dialog));
            Piece(GroupButton(ui, "MOVE", SceneUi.MutedButtonColor, 64, 28), EditorGroupPart.RowKind, row, Place(EditorGroupPart.RowKind, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowStart, row, Place(EditorGroupPart.RowStart, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowEnd, row, Place(EditorGroupPart.RowEnd, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowA, row, Place(EditorGroupPart.RowA, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowB, row, Place(EditorGroupPart.RowB, row, dialog));
            Piece(
                GroupButton(ui, "LIN", SceneUi.MutedButtonColor, 74, 28)
                    .Tooltip("Click to cycle the easing curve: linear, ease in, ease out, ease in-out"),
                EditorGroupPart.RowEase,
                row,
                Place(EditorGroupPart.RowEase, row, dialog));
            Piece(GroupButton(ui, "X", SceneUi.MutedButtonColor, 30, 28), EditorGroupPart.RowDelete, row, Place(EditorGroupPart.RowDelete, row, dialog));
        }

        UiNode palette = ui;
        Rect2 paletteArea = Place(EditorGroupPart.PalettePanel, 0, dialog);
        Piece(
            palette.Rect(Palette.Surface)
                .Size(paletteArea.Size)
                .At(UiLayout.Hidden)
                .Input(),
            EditorGroupPart.PalettePanel,
            0,
            paletteArea);
        Piece(
            palette.Text("COLOUR", 13, SceneUi.DimTextColor, TextAlign.Left)
                .Size(90, 24)
                .At(UiLayout.Hidden),
            EditorGroupPart.PaletteTitle,
            0,
            Place(EditorGroupPart.PaletteTitle, 0, dialog));
        Piece(GroupButton(palette, "X", SceneUi.MutedButtonColor, 30, 24), EditorGroupPart.PaletteClose, 0, Place(EditorGroupPart.PaletteClose, 0, dialog));
        for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
        {
            Color swatchColor = EditorGroupPalette.Swatches[i];
            Piece(
                palette.Rect(swatchColor)
                    .Size(new Vector2(44, 30))
                    .At(UiLayout.Hidden)
                    .Input()
                    .With(ButtonVisual.Create(swatchColor)),
                EditorGroupPart.PaletteSwatch,
                i,
                Place(EditorGroupPart.PaletteSwatch, i, dialog));
        }

        for (int i = 0; i < EditorGroupPalette.Alphas.Length; i++)
        {
            Piece(
                GroupButton(palette, EditorGroupPalette.AlphaLabel(i), SceneUi.MutedButtonColor, 68, 26),
                EditorGroupPart.PaletteAlpha,
                i,
                Place(EditorGroupPart.PaletteAlpha, i, dialog));
        }
    }

    private static Rect2 Place(EditorGroupPart part, int row, Vector2 dialog) =>
        EditorGroupLayout.Place(part, row, dialog);

    private static Entity Piece(Entity entity, EditorGroupPart part, int row, Rect2 _)
    {
        entity.AddComponent(new EditorGroupPiece(part, row));
        if (part == EditorGroupPart.PalettePanel)
            entity.AddTag<UiOverlay>();
        else if (part is EditorGroupPart.PaletteTitle or EditorGroupPart.PaletteClose
                 or EditorGroupPart.PaletteSwatch or EditorGroupPart.PaletteAlpha)
        {
            entity.AddTag<UiOverlayItem>();
        }

        return entity;
    }

    private static UiNode GroupButton(
        UiNode parent,
        string label,
        Color color,
        float width,
        float height) =>
        parent.Button(label, 12, color)
            .Size(width, height)
            .At(UiLayout.Hidden);

    private static Entity GroupField(
        UiNode parent,
        string value,
        bool numeric,
        float width) =>
        parent.Rect(Palette.SurfaceRaised, value, new Vector2(width, 28))
            .At(UiLayout.Hidden)
            .Input()
            .With(new TextStyle(13, SceneUi.TextColor))
            .Align(TextAlign.Left)
            .With(new TextField(value, numeric));
}
