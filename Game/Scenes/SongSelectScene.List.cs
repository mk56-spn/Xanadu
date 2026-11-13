using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Strings;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Scenes;

/// <summary>The staggered level list: scrolling rows around the selected level.</summary>
public static partial class SongSelectScene
{
    private const int RowCount = 9;

    /// <summary>The row holding the selected level when the list is at rest.</summary>
    private const int SelectedRow = 4;

    /// <summary>The visible slot, out of seven, that the selection rests in.</summary>
    private const int SelectedSlot = 3;

    /// <summary>Exponential catch-up rate of the scroll, per second.</summary>
    public const float ScrollRate = 16f;
    private static readonly Vector2 RowSize = new(500, 48);
    private static readonly Color Transparent = new(0, 0, 0, 0);
    private static readonly Color RowColor = Colors.Black;

    // Slots -1 and 7 sit just off either end and exist only for the slide.
    private static readonly float[] RowLeft =
        [47.1f, 73.877f, 100.68f, 124.695f, 152.827f, 142.029f, 115.577f, 80.889f, 50f];

    // Evenly spaced now the rows are slim; slot 3, the selection, keeps its place.
    private const float RowPitch = 60;
    private const float SelectedTop = 500.705f;

    private static readonly float[] RowTop =
        [.. Enumerable.Range(0, 9).Select(i => SelectedTop + (i - 4) * RowPitch)];

    private const float RowChamfer = 14;
    private const int NameSize = 26;
    private const int SongSize = 22;
    private const float RowGap = 10;
    private const float RowTextHeight = 34;

    private const float RowOpacity = 0.8f;

    /// <summary>Where the text starts: the canvas insets it by a little more.</summary>
    private const float RowPadding = 15.24f - TextInset;
    private const float RowBorder = 3;
    private static readonly Color RowBorderColor = new("#999999");

    /// <summary>A row whose song the catalog knows but that isn't downloaded: its name and song in grey.</summary>
    private static readonly Color MissingName = new("#6b6b6b");
    private static readonly Color MissingSong = new("#565656");

    private const float TrackTop = 345.748f;
    private const float TrackBottom = 738.57f;
    private const float TrackX = 40.991f;
    private const float MinThumb = 40;
    private const float ListBarLeft = 12.202f;
    private const float ListBarRight = 69.781f;

    /// <summary>
    /// The list's scrollbar. The carousel always scrolls, keeping the selection in
    /// its slot, so the thumb is one level's share of the track rather than the
    /// share on screen: any list of two or more levels can be dragged.
    /// </summary>
    public static VerticalSlider ListSlider(in SongSelectState state) =>
        new(TrackTop, TrackBottom, 1f / Mathf.Max(state.Catalog.Count, 1), MinThumb);

    /// <summary>How far down the list is scrolled, from 0 to 1.</summary>
    public static float ListValue(in SongSelectState state) =>
        state.Catalog.Count > 1 ? state.Scroll / (state.Catalog.Count - 1) : 0;

    private readonly record struct ListViews(
        List<Entity> Tiles,
        Entity Thumb,
        Entity Track,
        Entity EmptyTitle,
        Entity EmptyHint);

    /// <summary>The name label of a carousel row.</summary>
    public static Entity TileName(in SongSelectState state, int row) =>
        state.Tiles[row].ChildEntities[0];

    /// <summary>The song label of a carousel row.</summary>
    public static Entity TileSong(in SongSelectState state, int row) =>
        state.Tiles[row].ChildEntities[1];

    private static ListViews BuildList(UiNode ui, bool loadAssets)
    {
        // Each row is a chamfered container: the map's name, then the song's name
        // in brackets, flowing left to right.
        var tiles = new List<Entity>();
        for (int row = 0; row < RowCount; row++)
        {
            UiNode tile = ui.Row()
                .Size(RowSize)
                .Gap(RowGap)
                .Fill(RowColor)
                .Input()
                .Pad(RowPadding)
                .Main(StackMainAlignment.Start)
                .With(ButtonVisual.Create(RowColor))
                .With(new SongSelectTile(-1))
                .Chamfer(RowChamfer)
                .Border(Fade(RowBorderColor, RowOpacity), RowBorder);
            tiles.Add(tile.Entity);

            // A tile's children are its name, then its song.
            RowText(tile, NameSize, Colors.White);
            RowText(tile, SongSize, Gold)
                .Glow(new Color(Gold, 0.6f), radius: 8, intensity: 1.4f);
        }

        // The selection stays in its slot while the rows slide through it.
        float left = RowLeft[SelectedSlot + 1];
        float top = RowTop[SelectedSlot + 1];
        ui.Shape(
                UiOutline.Points(new Rect2(left, top, RowSize.X, RowSize.Y), 0, RowChamfer),
                Transparent,
                new Color(Gold, RowOpacity),
                RowBorder)
            .Raised();

        // The marker beside it, centred on the slim row.
        float middle = top + RowSize.Y / 2;
        ui.Shape(
            [new(86.396f, middle), new(132.38f, middle)],
            Transparent,
            Gold,
            4,
            closed: false);
        ui.Shape(
            [new(132.38f, middle - 20), new(132.38f, middle + 20)],
            Transparent,
            Gold,
            4,
            closed: false);

        BuildScrollbar(ui, out Entity thumb, out Entity track);
        Entity emptyTitle = EmptyMessage(
            ui,
            new Vector2(399.198f, 516),
            new Vector2(460, 32),
            LocalizedText.Get(SongSelectStrings.NoLevels),
            26,
            Gold);
        Entity emptyHint = EmptyMessage(
            ui,
            new Vector2(399.198f, 556),
            EmptyHintSize,
            LocalizedText.Get(SongSelectStrings.CreateLevel),
            16,
            Colors.White,
            clickable: true);
        emptyHint.AddComponent(UiCommand.Of(SceneActionId.Edit));
        return new ListViews(
            tiles,
            thumb,
            track,
            emptyTitle,
            emptyHint);
    }

    /// <summary>The two lines the empty carousel shows in place of its rows.</summary>
    private static Entity EmptyMessage(
        UiNode parent,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        bool clickable = false) =>
        parent.Rect(Transparent, text, size)
            .At(center)
            .Input(clickable)
            .With(new TextStyle(fontSize, color))
            .Raised();

    private static void BuildScrollbar(UiNode parent, out Entity thumb, out Entity track)
    {
        parent.Shape(
            RoundedRect(12.202f, 319.569f, 69.781f, 764.748f, 14.489f),
            ListBackdropTrack);
        parent.Shape(
            [new(TrackX, TrackTop), new(TrackX, TrackBottom)],
            default,
            Colors.Black,
            20,
            closed: false);
        Entity marker = parent.Shape(
                [new(TrackX, TrackTop), new(TrackX, TrackBottom)],
                default,
                Gold,
                24,
                closed: false)
            .With(new ShapeGlow(new Color(Gold, 0.6f), radius: 10, intensity: 1.5f))
            .Raised();
        thumb = marker;

        // Invisible, as wide as the backdrop: what the pointer actually presses.
        track = parent.Rect(
                Transparent,
                "",
                new Vector2(ListBarRight - ListBarLeft, TrackBottom - TrackTop))
            .At(new Vector2((ListBarLeft + ListBarRight) / 2, (TrackTop + TrackBottom) / 2))
            .Input()
            .Raised();
    }

    private static readonly Color ListBackdropTrack = new(0, 0, 0, 0.7f);

    /// <summary>A bold left-aligned label inside a row; its width is set when the row is bound.</summary>
    private static UiNode RowText(UiNode tile, int fontSize, Color color) =>
        tile.Text("", fontSize, color, TextAlign.Left)
            .Size(RowLabelWidth("", fontSize), RowTextHeight)
            .Bold();

    /// <summary>The width a bold label needs, with the canvas's padding either side.</summary>
    private static float RowLabelWidth(string text, int fontSize) =>
        UiTextMetrics.Width(text, fontSize, bold: true) + TextInset * 2;

    private static Vector2[] RoundedRect(
        float left,
        float top,
        float right,
        float bottom,
        float radius)
    {
        const int steps = 6;
        var points = new List<Vector2>();
        (Vector2 corner, float start)[] corners =
        [
            (new Vector2(right - radius, top + radius), -90),
            (new Vector2(right - radius, bottom - radius), 0),
            (new Vector2(left + radius, bottom - radius), 90),
            (new Vector2(left + radius, top + radius), 180),
        ];
        foreach ((Vector2 corner, float start) in corners)
        {
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.DegToRad(start + 90f * i / steps);
                points.Add(corner + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius));
            }
        }

        return points.ToArray();
    }

    /// <summary>
    /// Advances the animated scroll toward the selection and re-lays the rows.
    /// Returns false once the list is at rest.
    /// </summary>
    public static bool AdvanceScroll(ref SongSelectState state, float delta)
    {
        float distance = state.Selected - state.Scroll;
        if (distance == 0)
            return false;

        state.Scroll = Mathf.Abs(distance) < 0.002f
            ? state.Selected
            : state.Scroll + distance * (1 - Mathf.Exp(-ScrollRate * delta));
        RefreshList(ref state);
        return true;
    }

    /// <summary>How many levels a drag between two heights moves the list; pulling up moves it forward.</summary>
    public static float LevelsDragged(float fromY, float toY) => (fromY - toY) / RowPitch;

    /// <summary>Puts the list at a scroll directly, kept within the catalog, and re-lays the rows.</summary>
    public static void SetScroll(ref SongSelectState state, float scroll)
    {
        state.Scroll = Mathf.Clamp(scroll, 0, Mathf.Max(state.Catalog.Count - 1, 0));
        RefreshList(ref state);
    }

    /// <summary>
    /// Lays out every row for the current scroll. Row r shows the level
    /// r - 4 places from floor(scroll) and sits at a fractional slot, so a
    /// whole-number scroll puts the selection in the highlighted slot.
    /// </summary>
    private static void RefreshList(ref SongSelectState state)
    {
        IReadOnlyList<LevelEntry> catalog = state.Catalog;
        int whole = Mathf.FloorToInt(state.Scroll);
        float fraction = state.Scroll - whole;

        for (int row = 0; row < RowCount; row++)
        {
            int index = whole + row - SelectedRow;
            bool valid = index >= 0 && index < catalog.Count;
            float slot = row - 1 - fraction;
            float alpha = valid ? SlotAlpha(slot) : 0;

            ref SongSelectTile tile = ref state.Tiles[row].GetComponent<SongSelectTile>();
            if (tile.Index != (valid ? index : -1))
            {
                tile.Index = valid ? index : -1;
                BindRow(ref state, row, valid ? catalog[index] : null);
            }

            Vector2 corner = new(
                Interpolate(RowLeft, slot),
                Interpolate(RowTop, slot));
            Place(state.Tiles[row], corner + RowSize / 2);

            // A song that isn't downloaded is greyed out: there, but not playable.
            bool missing = valid && catalog[index].IsBlocked;
            SetTileColor(state.Tiles[row], Fade(RowColor, alpha * RowOpacity));
            SetBorderColor(state.Tiles[row], Fade(RowBorderColor, alpha * RowOpacity * (missing ? 0.5f : 1f)));
            UiText.SetColor(TileName(state, row), Fade(missing ? MissingName : Colors.White, alpha));
            UiText.SetColor(TileSong(state, row), Fade(missing ? MissingSong : Gold, alpha));
            SetGlowColor(TileSong(state, row), Fade(new Color(Gold, 0.6f), missing ? 0f : alpha));
        }

        VerticalSlider slider = ListSlider(state);
        float start = slider.ThumbTop(ListValue(state));
        // An empty list has nothing to scroll, so the thumb collapses to a point.
        Vector2[] span = catalog.Count == 0
            ? [new(TrackX, TrackTop)]
            : [new(TrackX, start), new(TrackX, start + slider.ThumbLength)];
        SetPoints(state.Thumb, span);
    }

    private static void SetPoints(Entity shape, Vector2[] points)
    {
        shape.GetComponent<UiShape>().Points = points;
        shape.AddTag<Dirty>();
    }

    private static void SetBorderColor(Entity entity, Color color)
    {
        ref UiBorder border = ref entity.GetComponent<UiBorder>();
        if (border.Color == color)
            return;

        border.Color = color;
        entity.AddTag<Dirty>();
    }

    private static void SetGlowColor(Entity entity, Color color)
    {
        ref TextGlow glow = ref entity.GetComponent<TextGlow>();
        if (glow.Color == color)
            return;

        glow.Color = color;
        entity.AddTag<Dirty>();
    }

    private static void BindRow(ref SongSelectState state, int row, LevelEntry? entry)
    {
        // The map's name, then the song's in brackets; short enough to share a row.
        string name = Fit(MapsetName(entry), 14);
        string song = entry is null ? "" : $"[{Fit(SongTitle(entry), 12)}]";
        FitLabel(TileName(state, row), name, NameSize);
        FitLabel(TileSong(state, row), song, SongSize);
    }

    /// <summary>Sets a label's text and gives it exactly the width it needs, so the row can flow.</summary>
    private static void FitLabel(Entity label, string text, int fontSize)
    {
        SceneUi.SetText(label, text);
        ref Rectangle rect = ref label.GetComponent<Rectangle>();
        var size = new Vector2(RowLabelWidth(text, fontSize), rect.Bounds.Size.Y);
        if (rect.Bounds.Size == size)
            return;

        rect.Bounds = new Rect2(-size / 2, size);
        label.Parent.AddTag<LayoutDirty>();
    }

    /// <summary>Linear lookup in a table whose first entry is slot -1.</summary>
    private static float Interpolate(float[] table, float slot)
    {
        float position = Mathf.Clamp(slot + 1, 0, table.Length - 1);
        int low = Mathf.Min((int)position, table.Length - 2);
        return Mathf.Lerp(table[low], table[low + 1], position - low);
    }

    /// <summary>Rows fade in over the slot above the list and out below it.</summary>
    private static float SlotAlpha(float slot) =>
        slot < 0 ? Mathf.Clamp(slot + 1, 0, 1) : Mathf.Clamp(7 - slot, 0, 1);

    private static Color Fade(Color color, float alpha) =>
        new(color, color.A * alpha);

    private static void Place(Entity entity, Vector2 position) => UiLayout.Move(entity, position);

    private static string MapsetName(LevelEntry? entry) =>
        entry is null
            ? ""
            : string.IsNullOrWhiteSpace(entry.Document.Name)
                ? Xanadu.Strings.LocalizedText.Get(Xanadu.Strings.SongSelectStrings.Untitled)
                : entry.Document.Name;

    private static string SongTitle(LevelEntry? entry) =>
        entry is null ? "" : entry.SongTitle ?? "SONG MISSING";

    private static string Fit(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
