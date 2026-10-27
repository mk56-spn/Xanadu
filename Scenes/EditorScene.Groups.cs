using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Scenes;

/// <summary>The groups grid in the editor's sidebar and the overlay screen a group opens.</summary>
public static partial class EditorScene
{
    /// <summary>Room the grid takes in the sidebar: its label, four rows of cells and a margin.</summary>
    public const float GroupGridHeight = EditorGroupLayout.GridHeight;

    /// <summary>The groups grid. Its cells are placed in a column of the sidebar; the layout system sets their rows.</summary>
    private static void BuildGroupGrid(UiBuilder ui, Entity editor, float centerX, float top)
    {
        const float cell = 40f;
        const float gap = 6f;
        float width = GroupLimits.Columns * cell + (GroupLimits.Columns - 1) * gap;
        float left = centerX - width / 2 + cell / 2;

        Entity label = SceneUi.Text(
            ui,
            new Vector2(centerX, top + 80),
            new Vector2(190, 20),
            "GROUPS",
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));

        var cells = new Entity[GroupLimits.Count];
        for (int i = 0; i < cells.Length; i++)
        {
            int column = i % GroupLimits.Columns;
            Entity button = ui.RectEntity(
                new Vector2(left + column * (cell + gap), top + 100),
                new Vector2(cell, 34),
                SceneUi.MutedButtonColor,
                (i + 1).ToString(),
                takesInput: true);
            button.AddComponent(new TextStyle(14, SceneUi.TextColor));
            button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
            button.AddComponent(new EditorGroupCell(i));
            button.AddComponent(new UiTooltip($"Edit group {i + 1}"));
            cells[i] = button;
        }

        Entity preview = ui.RectEntity(
            new Vector2(centerX, top + 300),
            new Vector2(150, 30),
            SceneUi.MutedButtonColor,
            "PREVIEW OFF",
            takesInput: true);
        preview.AddComponent(new TextStyle(12, SceneUi.TextColor));
        preview.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        preview.AddComponent(new UiTooltip("Show where the groups move and tint their members at the playhead"));

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
        Color panel = new("#0f172a");
        Color field = new("#16223a");

        UiBuilder scrim = SceneUi.Layer(entities, root, 975);
        Piece(scrim.RectEntity(UiLayout.Hidden, screen, new Color(0, 0, 0, 0.7f), takesInput: true),
            EditorGroupPart.Scrim, 0, new Rect2(Vector2.Zero, screen));

        UiBuilder back = SceneUi.Layer(entities, root, 976);
        Piece(back.RectEntity(UiLayout.Hidden, dialog, panel, takesInput: true), EditorGroupPart.Dialog, 0, Place(EditorGroupPart.Dialog, 0, dialog));

        UiBuilder tracks = SceneUi.Layer(entities, root, 977);
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Rect2 track = Place(EditorGroupPart.RowTrack, row, dialog);
            Piece(tracks.RectEntity(UiLayout.Hidden, track.Size, field), EditorGroupPart.RowTrack, row, track);
        }

        UiBuilder ticks = SceneUi.Layer(entities, root, 978);
        for (int slot = 0; slot < EditorGroupLayout.TickSlots; slot++)
        {
            Piece(
                ticks.RectEntity(UiLayout.Hidden, new Vector2(1, EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight), new Color("#1e293b")),
                EditorGroupPart.TickLine,
                slot,
                new Rect2(0, 0, 1, EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight));
        }

        Piece(
            ticks.RectEntity(UiLayout.Hidden, new Vector2(2, EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight), Colors.White),
            EditorGroupPart.PlayheadLine,
            0,
            Place(EditorGroupPart.PlayheadLine, 0, dialog));

        UiBuilder bars = SceneUi.Layer(entities, root, 979);
        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Rect2 track = Place(EditorGroupPart.RowTrack, row, dialog);
            Piece(
                bars.RectEntity(UiLayout.Hidden, new Vector2(10, track.Size.Y - 8), new Color("#38bdf8")),
                EditorGroupPart.RowBar,
                row,
                new Rect2(0, 0, 10, track.Size.Y - 8));
        }

        UiBuilder ui = SceneUi.Layer(entities, root, 980);
        for (int slot = 0; slot < EditorGroupLayout.LabelSlots; slot++)
        {
            Entity label = SceneUi.Text(ui, UiLayout.Hidden, new Vector2(70, 16), "", 11, SceneUi.DimTextColor, new Color(0, 0, 0, 0));
            Piece(label, EditorGroupPart.TickLabel, slot, new Rect2(0, 0, 70, 16));
        }

        Entity title = SceneUi.Text(ui, UiLayout.Hidden, new Vector2(190, 30), "GROUP", 22, SceneUi.AccentColor, new Color(0, 0, 0, 0));
        title.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(title, EditorGroupPart.Title, 0, Place(EditorGroupPart.Title, 0, dialog));

        Piece(GroupField(ui, "", false, 280), EditorGroupPart.NameField, 0, Place(EditorGroupPart.NameField, 0, dialog));
        Piece(GroupButton(ui, "DONE", SceneUi.ButtonColor, 100, 30), EditorGroupPart.Close, 0, Place(EditorGroupPart.Close, 0, dialog));

        Entity members = SceneUi.Text(ui, UiLayout.Hidden, new Vector2(190, 28), "", 13, SceneUi.TextColor, new Color(0, 0, 0, 0));
        members.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(members, EditorGroupPart.MemberText, 0, Place(EditorGroupPart.MemberText, 0, dialog));
        Piece(GroupButton(ui, "ADD SELECTED", SceneUi.ButtonColor, 130, 28), EditorGroupPart.AddSelected, 0, Place(EditorGroupPart.AddSelected, 0, dialog));
        Piece(GroupButton(ui, "REMOVE SELECTED", SceneUi.MutedButtonColor, 150, 28), EditorGroupPart.RemoveSelected, 0, Place(EditorGroupPart.RemoveSelected, 0, dialog));
        Piece(GroupButton(ui, "SELECT MEMBERS", SceneUi.MutedButtonColor, 140, 28), EditorGroupPart.SelectMembers, 0, Place(EditorGroupPart.SelectMembers, 0, dialog));
        Piece(GroupButton(ui, "+ MOVE", SceneUi.ButtonColor, 100, 28), EditorGroupPart.AddMove, 0, Place(EditorGroupPart.AddMove, 0, dialog));
        Piece(GroupButton(ui, "+ COLOR", SceneUi.ButtonColor, 100, 28), EditorGroupPart.AddColor, 0, Place(EditorGroupPart.AddColor, 0, dialog));
        Piece(GroupButton(ui, "SNAP", SceneUi.ButtonColor, 110, 28), EditorGroupPart.SnapToggle, 0, Place(EditorGroupPart.SnapToggle, 0, dialog));
        Entity solo = GroupButton(ui, "SOLO", SceneUi.MutedButtonColor, 88, 28);
        solo.AddComponent(new UiTooltip("Preview only this group"));
        Piece(solo, EditorGroupPart.SoloToggle, 0, Place(EditorGroupPart.SoloToggle, 0, dialog));
        Entity play = GroupButton(ui, "PLAY", SceneUi.ButtonColor, 70, 28);
        play.AddComponent(new UiTooltip("Play or pause the live preview"));
        Piece(play, EditorGroupPart.PlayToggle, 0, Place(EditorGroupPart.PlayToggle, 0, dialog));
        Piece(GroupButton(ui, "<", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollLeft, 0, Place(EditorGroupPart.ScrollLeft, 0, dialog));
        Piece(GroupButton(ui, ">", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollRight, 0, Place(EditorGroupPart.ScrollRight, 0, dialog));
        Piece(GroupButton(ui, "-", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomOut, 0, Place(EditorGroupPart.ZoomOut, 0, dialog));
        Piece(GroupButton(ui, "+", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomIn, 0, Place(EditorGroupPart.ZoomIn, 0, dialog));

        Entity selected = SceneUi.Text(
            ui,
            UiLayout.Hidden,
            new Vector2(170, 28),
            "NO EFFECT",
            11,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        selected.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(selected, EditorGroupPart.SelectionText, 0, Place(EditorGroupPart.SelectionText, 0, dialog));
        Entity target = GroupButton(ui, "TARGET", SceneUi.MutedButtonColor, 70, 28);
        target.AddComponent(new UiTooltip("Drag the selected move's offset on the playfield"));
        Piece(target, EditorGroupPart.SelectionTarget, 0, Place(EditorGroupPart.SelectionTarget, 0, dialog));
        Entity duplicate = GroupButton(ui, "DUP", SceneUi.MutedButtonColor, 60, 28);
        duplicate.AddComponent(new UiTooltip("Duplicate the selected effect"));
        Piece(duplicate, EditorGroupPart.SelectionDuplicate, 0, Place(EditorGroupPart.SelectionDuplicate, 0, dialog));
        Entity mute = GroupButton(ui, "MUTE", SceneUi.MutedButtonColor, 70, 28);
        mute.AddComponent(new UiTooltip("Mute or unmute the selected effect"));
        Piece(mute, EditorGroupPart.SelectionMute, 0, Place(EditorGroupPart.SelectionMute, 0, dialog));
        Entity nudgeLeft = GroupButton(ui, "◀", SceneUi.MutedButtonColor, 32, 28);
        nudgeLeft.AddComponent(new UiTooltip("Nudge the selected effect one beat left"));
        Piece(nudgeLeft, EditorGroupPart.SelectionNudgeLeft, 0, Place(EditorGroupPart.SelectionNudgeLeft, 0, dialog));
        Entity nudgeRight = GroupButton(ui, "▶", SceneUi.MutedButtonColor, 32, 28);
        nudgeRight.AddComponent(new UiTooltip("Nudge the selected effect one beat right"));
        Piece(nudgeRight, EditorGroupPart.SelectionNudgeRight, 0, Place(EditorGroupPart.SelectionNudgeRight, 0, dialog));
        Entity pick = GroupButton(ui, "PICK", SceneUi.MutedButtonColor, 54, 28);
        pick.AddComponent(new UiTooltip("Pick a colour for the selected colour effect"));
        Piece(pick, EditorGroupPart.SelectionPick, 0, Place(EditorGroupPart.SelectionPick, 0, dialog));

        Entity hint = SceneUi.Text(
            ui,
            UiLayout.Hidden,
            new Vector2(dialog.X - 40, 44),
            "Drag a bar to retime, its ends for START/END. EASE cycles the curve. Pick a row to use the selection bar.",
            11,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        hint.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(hint, EditorGroupPart.Hint, 0, Place(EditorGroupPart.Hint, 0, dialog));

        UiBuilder previewUi = SceneUi.Layer(entities, root, 981);
        Rect2 previewArea = Place(EditorGroupPart.PreviewPanel, 0, dialog);
        Piece(
            previewUi.RectEntity(UiLayout.Hidden, previewArea.Size, new Color("#0b1220"), takesInput: false),
            EditorGroupPart.PreviewPanel,
            0,
            previewArea);

        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Entity index = SceneUi.Text(ui, UiLayout.Hidden, new Vector2(24, 28), (row + 1).ToString(), 12, SceneUi.DimTextColor, new Color(0, 0, 0, 0));
            Piece(index, EditorGroupPart.RowIndex, row, Place(EditorGroupPart.RowIndex, row, dialog));
            Piece(GroupButton(ui, "MOVE", SceneUi.MutedButtonColor, 64, 28), EditorGroupPart.RowKind, row, Place(EditorGroupPart.RowKind, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowStart, row, Place(EditorGroupPart.RowStart, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowEnd, row, Place(EditorGroupPart.RowEnd, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowA, row, Place(EditorGroupPart.RowA, row, dialog));
            Piece(GroupField(ui, "", true, 64), EditorGroupPart.RowB, row, Place(EditorGroupPart.RowB, row, dialog));
            Entity ease = GroupButton(ui, "LIN", SceneUi.MutedButtonColor, 74, 28);
            ease.AddComponent(new UiTooltip("Click to cycle the easing curve: linear, ease in, ease out, ease in-out"));
            Piece(ease, EditorGroupPart.RowEase, row, Place(EditorGroupPart.RowEase, row, dialog));
            Piece(GroupButton(ui, "X", SceneUi.MutedButtonColor, 30, 28), EditorGroupPart.RowDelete, row, Place(EditorGroupPart.RowDelete, row, dialog));
        }
        UiBuilder palette = ui;
        Rect2 paletteArea = Place(EditorGroupPart.PalettePanel, 0, dialog);
        Piece(
            palette.RectEntity(UiLayout.Hidden, paletteArea.Size, new Color("#111827"), takesInput: true),
            EditorGroupPart.PalettePanel,
            0,
            paletteArea);
        Entity paletteTitle = SceneUi.Text(
            palette,
            UiLayout.Hidden,
            new Vector2(90, 24),
            "COLOUR",
            13,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        paletteTitle.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(paletteTitle, EditorGroupPart.PaletteTitle, 0, Place(EditorGroupPart.PaletteTitle, 0, dialog));
        Piece(GroupButton(palette, "X", SceneUi.MutedButtonColor, 30, 24), EditorGroupPart.PaletteClose, 0, Place(EditorGroupPart.PaletteClose, 0, dialog));
        for (int i = 0; i < EditorGroupPalette.Swatches.Length; i++)
        {
            Color swatchColor = EditorGroupPalette.Swatches[i];
            Entity swatch = palette.RectEntity(
                UiLayout.Hidden,
                new Vector2(44, 30),
                swatchColor,
                takesInput: true);
            swatch.AddComponent(ButtonVisual.Create(swatchColor));
            Piece(swatch, EditorGroupPart.PaletteSwatch, i, Place(EditorGroupPart.PaletteSwatch, i, dialog));
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

    private static Entity GroupButton(UiBuilder ui, string label, Color color, float width, float height)
    {
        Entity button = ui.RectEntity(UiLayout.Hidden, new Vector2(width, height), color, label, takesInput: true);
        button.AddComponent(new TextStyle(12, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(color));
        return button;
    }

    private static Entity GroupField(UiBuilder ui, string value, bool numeric, float width)
    {
        Entity field = ui.RectEntity(UiLayout.Hidden, new Vector2(width, 28), new Color("#16223a"), value, takesInput: true);
        field.AddComponent(new TextStyle(13, SceneUi.TextColor));
        field.AddComponent(new TextAlignment(TextAlign.Left));
        field.AddComponent(new TextField(value, numeric));
        return field;
    }
}
