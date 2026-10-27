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
        Piece(GroupButton(ui, "<", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollLeft, 0, Place(EditorGroupPart.ScrollLeft, 0, dialog));
        Piece(GroupButton(ui, ">", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ScrollRight, 0, Place(EditorGroupPart.ScrollRight, 0, dialog));
        Piece(GroupButton(ui, "-", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomOut, 0, Place(EditorGroupPart.ZoomOut, 0, dialog));
        Piece(GroupButton(ui, "+", SceneUi.MutedButtonColor, 40, 28), EditorGroupPart.ZoomIn, 0, Place(EditorGroupPart.ZoomIn, 0, dialog));

        Entity hint = SceneUi.Text(
            ui,
            UiLayout.Hidden,
            new Vector2(dialog.X - 40, 44),
            "Times in seconds. An effect tweens from START to END, then holds. Drag a bar to retime, or an end for START/END. EASE cycles it.",
            11,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        hint.AddComponent(new TextAlignment(TextAlign.Left));
        Piece(hint, EditorGroupPart.Hint, 0, Place(EditorGroupPart.Hint, 0, dialog));

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
    }

    private static Rect2 Place(EditorGroupPart part, int row, Vector2 dialog) =>
        EditorGroupLayout.Place(part, row, dialog);

    private static Entity Piece(Entity entity, EditorGroupPart part, int row, Rect2 _)
    {
        entity.AddComponent(new EditorGroupPiece(part, row));
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
