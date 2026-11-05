using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>The groups grid in the editor's sidebar and the overlay screen a group opens.</summary>
public static class EditorSceneGroups
{
    /// <summary>Room the grid takes in the sidebar: its label, four rows of cells and a margin.</summary>
    public const float GroupGridHeight = EditorGroupLayout.GridHeight;

    /// <summary>
    /// The groups label and a compact grid of the group cells, flowing in the
    /// sidebar. A cell puts the selection into that group, or takes it out when
    /// every selected item is already in it; groups are opened from the bar.
    /// </summary>
    internal static void BuildGroupGrid(UiNode ui, Entity editor, float width)
    {
        Entity label = EditorSceneUi.SidebarLabel(ui, "GROUPS", width, 14);

        var cells = new Entity[GroupLimits.Count];
        UiNode grid = ui.Grid(
            GroupLimits.Columns,
            gap: EditorGroupLayout.CellGap,
            rowGap: EditorGroupLayout.CellGap);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = grid.Button(
                    (i + 1).ToString(),
                    UiType.Small,
                    SceneUi.MutedButtonColor)
                .Size(EditorGroupLayout.CellWidth, EditorGroupLayout.CellHeight)
                .With(new EditorGroupCell(i))
                .Tooltip($"Put the selection in group {i + 1}, or take it out if it's all in already")
                .Entity;
        }

        editor.AddComponent(new EditorGroupGrid(label, cells));
    }

    /// <summary>
    /// The bar pinned at the top centre of the view, in sections split by thin
    /// separators: the group it points at with − and + and EDIT (which opens
    /// that group's panel under the bar) and TAGS, which shows items' groups beside them │ the grid menu │ the music menu and
    /// advance-on-placement │ the player path │ the layers panel.
    /// </summary>
    internal static void BuildGroupBar(EntityStore entities, Entity root, Entity editor, Vector2 screen)
    {
        Rect2 area = EditorGroupLayout.BarArea(screen);
        UiNode layer = UiNode.Layer(entities, root, UiLayers.EditorGroupBar, null, UiStage.Full);
        UiNode bar = layer.Row()
            .Size(area.Size)
            .Gap(6)
            .Pad(4)
            .Fill(SceneUi.PanelColor)
            .Input()
            .At(area.GetCenter());

        Entity label = bar.Text("GROUP 1", 13, SceneUi.TextColor, TextAlign.Left)
            .Size(120, 28)
            .Entity;
        Entity previous = bar.Button("-", 12, SceneUi.EditorButtonColor)
            .Size(30, 28)
            .Tooltip("The previous group")
            .Entity;
        Entity next = bar.Button("+", 12, SceneUi.EditorButtonColor)
            .Size(30, 28)
            .Tooltip("The next group")
            .Entity;
        Entity edit = bar.Button("EDIT", 12, SceneUi.EditorButtonColor)
            .Size(70, 28)
            .Tooltip("Open this group's effects and timeline")
            .Entity;
        GroupTagsSwitch.Attach(bar.Button(GroupTagsSwitch.Label, 12, SceneUi.MutedButtonColor)
            .Size(56, 28)
            .Entity);
        editor.AddComponent(new EditorGroupBar(label, previous, next, edit));

        Separator(bar);
        Entity gridButton = bar.IconButton("GR", IconDir + "grid.svg", new Vector2(36, 28), SceneUi.EditorButtonColor, 11)
            .Tooltip("Grid: show it, and snap items to it")
            .Entity;
        Separator(bar);
        Entity musicButton = bar.IconButton("♪", IconDir + "music.svg", new Vector2(36, 28), SceneUi.EditorButtonColor, 13)
            .Tooltip("Music snap: place notes on the beat grid, and how fine it is")
            .Entity;
        EditorAutoAdvance.Attach(bar.Button(EditorAutoAdvance.Label, 12, SceneUi.MutedButtonColor)
            .Size(52, 28)
            .Entity);
        Separator(bar);
        PlayerPathSwitch.Attach(bar.Button(PlayerPathSwitch.Label, 12, SceneUi.MutedButtonColor)
            .Size(56, 28)
            .Entity);
        Separator(bar);
        Entity layersButton = bar.Button("LAYERS", 12, SceneUi.EditorButtonColor)
            .Size(70, 28)
            .Tooltip("The layers: hide, lock or solo them, add one, name one, select what's on one")
            .Entity;
        EditorSceneLayers.BuildPanel(entities, root, editor, layersButton);

        Entity gridMenu = Menu(layer, 2).Entity;
        UiNode gridItems = UiNode.Wrap(gridMenu);
        Entity showGrid = ViewportGrid.Attach(gridItems.Button(ViewportGrid.Label, 12, SceneUi.MutedButtonColor)
            .Size(MenuItemWidth, 28)
            .Entity);
        Relabel(showGrid, "GRID ON", "GRID OFF");
        Entity gridSnap = ViewportSnap.Attach(
            gridItems.Button(ViewportSnap.Label, 12, SceneUi.MutedButtonColor)
                .Size(MenuItemWidth, 28)
                .Entity,
            "Snap items to the grid. Off by default: items go exactly where they are put",
            on: false);
        Relabel(gridSnap, "GRID SNAP ON", "GRID SNAP OFF");

        Entity musicMenu = Menu(layer, 2).Entity;
        UiNode musicItems = UiNode.Wrap(musicMenu);
        EditorMusicSnap.Attach(musicItems.Button(EditorMusicSnap.OnLabel, 12, SceneUi.MutedButtonColor)
            .Size(MenuItemWidth, 28)
            .Entity);
        UiNode divisions = musicItems.Row().Size(MenuItemWidth, 28).Gap(4);
        var divisionButtons = new Entity[EditorViewBar.Divisions.Length];
        for (int i = 0; i < divisionButtons.Length; i++)
        {
            (int division, string text) = EditorViewBar.Divisions[i];
            divisionButtons[i] = divisions.Button(text, 12, SceneUi.EditorButtonColor)
                .Size((MenuItemWidth - 8) / 3f, 28)
                .Tooltip($"Snap notes to {text} notes")
                .Entity;
        }

        editor.AddComponent(new EditorViewBar(gridButton, gridMenu, musicButton, musicMenu, divisionButtons));
    }

    private const string IconDir = "res://Assets/Icons/";
    private const float MenuItemWidth = 150f;

    /// <summary>A thin upright line between the bar's sections.</summary>
    private static void Separator(UiNode bar) =>
        bar.Rect(Palette.BorderStrong, size: new Vector2(1, 22));

    /// <summary>A hidden drop-down under one of the bar's buttons; <c>EditorViewBarSystem</c> places and shows it.</summary>
    private static UiNode Menu(UiNode layer, int rows) =>
        layer.Column()
            .Size(MenuItemWidth + 12, 12 + rows * 28 + (rows - 1) * 4)
            .Gap(4)
            .Pad(6)
            .Fill(Palette.Surface)
            .Input()
            .Hidden();

    private static void Relabel(Entity button, string on, string off)
    {
        ref Toggle toggle = ref button.GetComponent<Toggle>();
        toggle.OnText = on;
        toggle.OffText = off;
    }

    /// <summary>
    /// The group overlay: a nearly see-through panel docked to the top of the
    /// editor viewport, so the field (where the groups play live) stays in view.
    /// Two toolbars, one for the group and its members and one for its effects
    /// and the timeline's controls, sit over a table of the effects and a
    /// timeline of them. Every piece starts hidden; <c>EditorGroupSystem</c>
    /// places them from <see cref="EditorGroupLayout"/> while a group is open.
    /// </summary>
    internal static void BuildGroupOverlay(EntityStore entities, Entity root, Vector2 screen)
    {
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Color panel = Palette.SurfaceDeep;
        Color field = Palette.SurfaceRaised;

        UiNode scrim = UiNode.Layer(entities, root, UiLayers.EditorGroupsScrim, null, UiStage.Full);
        Piece(
            // Catches presses outside the panel without dimming the field.
            scrim.Rect(new Color(0, 0, 0, 0f))
                .Size(screen)
                .Hidden()
                .Input(),
            EditorGroupPart.Scrim,
            0,
            new Rect2(Vector2.Zero, screen));

        UiNode back = UiNode.Layer(entities, root, UiLayers.EditorGroupsBack, null, UiStage.Full);
        Piece(
            back.Rect(Palette.WithAlpha(panel, EditorGroupLayout.BackingAlpha)).Size(dialog).Hidden().Input(),
            EditorGroupPart.Dialog,
            0,
            Place(EditorGroupPart.Dialog, 0, dialog));

        UiNode ticks = UiNode.Layer(entities, root, UiLayers.EditorGroupsTicks, null, UiStage.Full);
        for (int slot = 0; slot < EditorGroupLayout.TickSlots; slot++)
        {
            Piece(
                ticks.Rect(Palette.SurfaceAlt)
                    .Size(new Vector2(
                        1,
                        EditorGroupLayout.MaxRows * EditorGroupLayout.RowHeight))
                    .Hidden(),
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
                .Hidden(),
            EditorGroupPart.PlayheadLine,
            0,
            Place(EditorGroupPart.PlayheadLine, 0, dialog));

        UiNode ui = UiNode.Layer(entities, root, UiLayers.EditorGroups, null, UiStage.Full);
        for (int slot = 0; slot < EditorGroupLayout.LabelSlots; slot++)
        {
            Piece(
                ui.Text("", 11, SceneUi.DimTextColor)
                    .Size(70, 16)
                    .Hidden(),
                EditorGroupPart.TickLabel,
                slot,
                new Rect2(0, 0, 70, 16));
        }

        Piece(
            ui.Text("GROUP", 18, SceneUi.AccentColor, TextAlign.Left)
                .Size(92, 28)
                .Hidden(),
            EditorGroupPart.Title,
            0,
            Place(EditorGroupPart.Title, 0, dialog));

        Piece(
            GroupField(ui, "", false, 200),
            EditorGroupPart.NameField,
            0,
            Place(EditorGroupPart.NameField, 0, dialog));
        Piece(
            GroupButton(ui, "DONE", SceneUi.EditorButtonColor, 84, 28),
            EditorGroupPart.Close,
            0,
            Place(EditorGroupPart.Close, 0, dialog));

        Piece(
            ui.Text("", 13, SceneUi.TextColor, TextAlign.Left)
                .Size(150, 28)
                .Hidden(),
            EditorGroupPart.MemberText,
            0,
            Place(EditorGroupPart.MemberText, 0, dialog));
        Piece(
            GroupButton(ui, "+ SELECTED", SceneUi.EditorButtonColor, 110, 28)
                .Tooltip("Add the selected items to this group"),
            EditorGroupPart.AddSelected,
            0,
            Place(EditorGroupPart.AddSelected, 0, dialog));
        Piece(
            GroupButton(ui, "− SELECTED", SceneUi.EditorButtonColor, 110, 28)
                .Tooltip("Take the selected items out of this group"),
            EditorGroupPart.RemoveSelected,
            0,
            Place(EditorGroupPart.RemoveSelected, 0, dialog));
        Piece(
            GroupButton(ui, "SELECT", SceneUi.EditorButtonColor, 80, 28)
                .Tooltip("Select this group's members"),
            EditorGroupPart.SelectMembers,
            0,
            Place(EditorGroupPart.SelectMembers, 0, dialog));
        Piece(
            GroupButton(ui, "INTERIOR", SceneUi.MutedButtonColor, 110, 28)
                .Tooltip("Whether this group's colour reaches the interior of the selected blocks in it"),
            EditorGroupPart.InteriorLink,
            0,
            Place(EditorGroupPart.InteriorLink, 0, dialog));
        Piece(
            GroupButton(ui, "OUTLINE", SceneUi.MutedButtonColor, 110, 28)
                .Tooltip("Whether this group's colour reaches the outline of the selected blocks in it"),
            EditorGroupPart.OutlineLink,
            0,
            Place(EditorGroupPart.OutlineLink, 0, dialog));
        Piece(GroupButton(ui, "+ MOVE", SceneUi.EditorButtonColor, 84, 28), EditorGroupPart.AddMove, 0, Place(EditorGroupPart.AddMove, 0, dialog));
        Piece(GroupButton(ui, "+ COLOR", SceneUi.EditorButtonColor, 84, 28), EditorGroupPart.AddColor, 0, Place(EditorGroupPart.AddColor, 0, dialog));
        Piece(
            GroupButton(ui, "BUF", SceneUi.EditorButtonColor, 84, 28)
                .Tooltip("Draw this group's members into one buffer so they fade as one image; only on one layer, and only for items in no other group"),
            EditorGroupPart.BufferToggle,
            0,
            Place(EditorGroupPart.BufferToggle, 0, dialog));
        Piece(GroupButton(ui, "SNAP", SceneUi.EditorButtonColor, 96, 28), EditorGroupPart.SnapToggle, 0, Place(EditorGroupPart.SnapToggle, 0, dialog));
        Piece(
            GroupButton(ui, "SOLO", SceneUi.EditorButtonColor, 84, 28)
                .Tooltip("Apply only this group in the editor view"),
            EditorGroupPart.SoloToggle,
            0,
            Place(EditorGroupPart.SoloToggle, 0, dialog));
        Piece(
            GroupButton(ui, "PLAY", SceneUi.EditorButtonColor, 76, 28)
                .Tooltip("Play or pause the song; the groups play live in the editor view"),
            EditorGroupPart.PlayToggle,
            0,
            Place(EditorGroupPart.PlayToggle, 0, dialog));
        Piece(GroupButton(ui, "<", SceneUi.EditorButtonColor, 32, 28).Tooltip("Scroll the timeline earlier"), EditorGroupPart.ScrollLeft, 0, Place(EditorGroupPart.ScrollLeft, 0, dialog));
        Piece(GroupButton(ui, ">", SceneUi.EditorButtonColor, 32, 28).Tooltip("Scroll the timeline later"), EditorGroupPart.ScrollRight, 0, Place(EditorGroupPart.ScrollRight, 0, dialog));
        Piece(GroupButton(ui, "-", SceneUi.EditorButtonColor, 32, 28).Tooltip("Zoom the timeline out"), EditorGroupPart.ZoomOut, 0, Place(EditorGroupPart.ZoomOut, 0, dialog));
        Piece(GroupButton(ui, "+", SceneUi.EditorButtonColor, 32, 28).Tooltip("Zoom the timeline in"), EditorGroupPart.ZoomIn, 0, Place(EditorGroupPart.ZoomIn, 0, dialog));

        Piece(
            ui.Text("", 11, SceneUi.DimTextColor, TextAlign.Left)
                .Size(210, 28)
                .Hidden(),
            EditorGroupPart.ShaderText,
            0,
            Place(EditorGroupPart.ShaderText, 0, dialog));
        Piece(
            GroupButton(ui, "SHD", SceneUi.EditorButtonColor, 64, 28)
                .Tooltip("The shader slot this group's item-shader track draws with; 0 is off"),
            EditorGroupPart.ShaderSlot,
            0,
            Place(EditorGroupPart.ShaderSlot, 0, dialog));
        Piece(
            GroupButton(ui, "PARAM", SceneUi.MutedButtonColor, 130, 28)
                .Tooltip("The instance uniform of that shader to key"),
            EditorGroupPart.ShaderParam,
            0,
            Place(EditorGroupPart.ShaderParam, 0, dialog));
        Piece(
            GroupButton(ui, "CH", SceneUi.MutedButtonColor, 56, 28)
                .Tooltip("Which component of the uniform the buttons change"),
            EditorGroupPart.ShaderChannel,
            0,
            Place(EditorGroupPart.ShaderChannel, 0, dialog));
        Piece(
            GroupButton(ui, "−", SceneUi.MutedButtonColor, 30, 28)
                .Tooltip("Lower the value at the playhead and key it"),
            EditorGroupPart.ShaderDown,
            0,
            Place(EditorGroupPart.ShaderDown, 0, dialog));
        Piece(
            ui.Text("", 11, SceneUi.TextColor, TextAlign.Left)
                .Size(150, 28)
                .Hidden(),
            EditorGroupPart.ShaderValue,
            0,
            Place(EditorGroupPart.ShaderValue, 0, dialog));
        Piece(
            GroupButton(ui, "+", SceneUi.MutedButtonColor, 30, 28)
                .Tooltip("Raise the value at the playhead and key it"),
            EditorGroupPart.ShaderUp,
            0,
            Place(EditorGroupPart.ShaderUp, 0, dialog));
        Piece(
            GroupButton(ui, "CLEAR", SceneUi.MutedButtonColor, 64, 28)
                .Tooltip("Remove every key of the selected uniform"),
            EditorGroupPart.ShaderClear,
            0,
            Place(EditorGroupPart.ShaderClear, 0, dialog));
        Piece(
            GroupButton(ui, "TRACK", SceneUi.MutedButtonColor, 76, 28)
                .Tooltip("Which track these controls edit: the group's item shader, or its buffer"),
            EditorGroupPart.BufferTrack,
            0,
            Place(EditorGroupPart.BufferTrack, 0, dialog));
        Piece(
            GroupButton(ui, "TINT", SceneUi.MutedButtonColor, 64, 28)
                .Tooltip("Cycle the buffer's tint"),
            EditorGroupPart.BufferTint,
            0,
            Place(EditorGroupPart.BufferTint, 0, dialog));
        Piece(
            GroupButton(ui, "A−", SceneUi.MutedButtonColor, 30, 28).Tooltip("Lower the buffer's alpha"),
            EditorGroupPart.BufferAlphaDown,
            0,
            Place(EditorGroupPart.BufferAlphaDown, 0, dialog));
        Piece(
            GroupButton(ui, "A+", SceneUi.MutedButtonColor, 30, 28).Tooltip("Raise the buffer's alpha"),
            EditorGroupPart.BufferAlphaUp,
            0,
            Place(EditorGroupPart.BufferAlphaUp, 0, dialog));
        Piece(
            GroupButton(ui, "BLEND", SceneUi.MutedButtonColor, 84, 28).Tooltip("Cycle how the buffer blends into its layer"),
            EditorGroupPart.BufferBlend,
            0,
            Place(EditorGroupPart.BufferBlend, 0, dialog));

        Piece(
            ui.Text("NO EFFECT", 11, SceneUi.DimTextColor, TextAlign.Left)
                .Size(150, 28)
                .Hidden(),
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
            GroupButton(ui, "DUP", SceneUi.MutedButtonColor, 52, 28)
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
            GroupButton(ui, "◀", SceneUi.MutedButtonColor, 30, 28)
                .Tooltip("Nudge the selected effect one beat left"),
            EditorGroupPart.SelectionNudgeLeft,
            0,
            Place(EditorGroupPart.SelectionNudgeLeft, 0, dialog));
        Piece(
            GroupButton(ui, "▶", SceneUi.MutedButtonColor, 30, 28)
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
                    "Drag a bar to retime it, or its ends for START/END. EASE cycles the curve. Pick a row to use the effect tools.",
                    11,
                    SceneUi.DimTextColor,
                    TextAlign.Left)
                .Size(dialog.X - 2 * EditorGroupLayout.Pad, 20)
                .Hidden(),
            EditorGroupPart.Hint,
            0,
            Place(EditorGroupPart.Hint, 0, dialog));

        for (int row = 0; row < EditorGroupLayout.MaxRows; row++)
        {
            Piece(
                ui.Text((row + 1).ToString(), 12, SceneUi.DimTextColor)
                    .Size(24, 28)
                    .Hidden(),
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
                .Hidden()
                .Input(),
            EditorGroupPart.PalettePanel,
            0,
            paletteArea);
        Piece(
            palette.Text("COLOUR", 13, SceneUi.DimTextColor, TextAlign.Left)
                .Size(90, 24)
                .Hidden(),
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
                    .Hidden()
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
            .Hidden();

    private static Entity GroupField(
        UiNode parent,
        string value,
        bool numeric,
        float width) =>
        parent.Rect(Palette.SurfaceRaised, value, new Vector2(width, 28))
            .Hidden()
            .Input()
            .With(new TextStyle(13, SceneUi.TextColor))
            .Align(TextAlign.Left)
            .With(new TextField(value, numeric));
}
