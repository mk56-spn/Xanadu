using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Strings;
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

    private const float TrackTop = 345.748f;
    private const float TrackBottom = 738.57f;
    private const float TrackX = 40.991f;
    private const float MinThumb = 40;

    private readonly record struct ListViews(
        List<Entity> Tiles,
        List<Entity> Names,
        List<Entity> Songs,
        Entity Thumb,
        Entity EmptyTitle,
        Entity EmptyHint);

    private static ListViews BuildList(UiBuilder ui, bool loadAssets)
    {
        // Each row is a chamfered container: the map's name, then the song's name
        // in brackets, flowing left to right.
        var tiles = new List<Entity>();
        var names = new List<Entity>();
        var songs = new List<Entity>();
        for (int row = 0; row < RowCount; row++)
        {
            ui.Container(
                RowSize / 2,
                RowSize,
                StackDirection.Horizontal,
                spacing: RowGap,
                color: RowColor,
                takesInput: true,
                padding: RowPadding,
                main: StackMainAlignment.Start);
            Entity tile = ui.Current;
            tile.AddComponent(ButtonVisual.Create(RowColor));
            tile.AddComponent(new SongSelectTile(-1));
            tile.AddComponent(new UiChamfer(RowChamfer));
            tile.AddComponent(new UiBorder(Fade(RowBorderColor, RowOpacity), RowBorder));
            tiles.Add(tile);

            Entity name = RowText(ui, NameSize, Colors.White);
            names.Add(name);

            Entity song = RowText(ui, SongSize, Gold);
            song.AddComponent(new TextGlow(new Color(Gold, 0.6f), radius: 8, intensity: 1.4f));
            songs.Add(song);
            ui.End();
        }

        // The selection stays in its slot while the rows slide through it.
        float left = RowLeft[SelectedSlot + 1];
        float top = RowTop[SelectedSlot + 1];
        Entity outline = ui.ShapeEntity(
            UiOutline.Points(new Rect2(left, top, RowSize.X, RowSize.Y), 0, RowChamfer),
            Transparent,
            new Color(Gold, RowOpacity),
            RowBorder);
        outline.AddTag<UiRaised>();

        // The marker beside it, centred on the slim row.
        float middle = top + RowSize.Y / 2;
        ui.ShapeEntity(
            [new(86.396f, middle), new(132.38f, middle)],
            Transparent,
            Gold,
            4,
            closed: false);
        ui.ShapeEntity(
            [new(132.38f, middle - 20), new(132.38f, middle + 20)],
            Transparent,
            Gold,
            4,
            closed: false);

        BuildScrollbar(ui, out Entity thumb);
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
        emptyHint.AddComponent(new SceneCommand(SceneActionId.Edit));
        return new ListViews(
            tiles,
            names,
            songs,
            thumb,
            emptyTitle,
            emptyHint);
    }

    /// <summary>The two lines the empty carousel shows in place of its rows.</summary>
    private static Entity EmptyMessage(
        UiBuilder ui,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        bool clickable = false)
    {
        Entity message = ui.RectEntity(
            center,
            size,
            Transparent,
            text,
            takesInput: clickable);
        message.AddComponent(new TextStyle(fontSize, color));
        message.AddTag<UiRaised>();
        return message;
    }

    private static void BuildScrollbar(UiBuilder ui, out Entity thumb)
    {
        ui.ShapeEntity(
            RoundedRect(12.202f, 319.569f, 69.781f, 764.748f, 14.489f),
            ListBackdropTrack);
        ui.ShapeEntity(
            [new(TrackX, TrackTop), new(TrackX, TrackBottom)],
            default,
            Colors.Black,
            20,
            closed: false);
        thumb = ui.ShapeEntity(
            [new(TrackX, TrackTop), new(TrackX, TrackBottom)],
            default,
            Gold,
            24,
            closed: false);
        thumb.AddComponent(new ShapeGlow(new Color(Gold, 0.6f), radius: 10, intensity: 1.5f));
        thumb.AddTag<UiRaised>();
    }

    private static readonly Color ListBackdropTrack = new(0, 0, 0, 0.7f);

    /// <summary>A bold left-aligned label inside a row; its width is set when the row is bound.</summary>
    private static Entity RowText(UiBuilder ui, int fontSize, Color color)
    {
        Entity text = SceneUi.Text(
            ui,
            Vector2.Zero,
            new Vector2(RowLabelWidth("", fontSize), RowTextHeight),
            "",
            fontSize,
            color,
            Transparent);
        text.AddComponent(new TextAlignment(TextAlign.Left));
        text.GetComponent<TextStyle>().Bold = true;
        return text;
    }

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

            SetTileColor(state.Tiles[row], Fade(RowColor, alpha * RowOpacity));
            SetBorderColor(state.Tiles[row], Fade(RowBorderColor, alpha * RowOpacity));
            SetTextColor(state.Names[row], Fade(Colors.White, alpha));
            SetTextColor(state.Songs[row], Fade(Gold, alpha));
            SetGlowColor(state.Songs[row], Fade(new Color(Gold, 0.6f), alpha));
        }

        float length = Mathf.Clamp(
            (TrackBottom - TrackTop) * 7 / Mathf.Max(catalog.Count, 1),
            MinThumb,
            TrackBottom - TrackTop);
        float travel = TrackBottom - TrackTop - length;
        float t = catalog.Count > 1
            ? Mathf.Clamp(state.Scroll / (catalog.Count - 1), 0, 1)
            : 0;
        float start = TrackTop + travel * t;
        // An empty list has nothing to scroll, so the thumb collapses to a point.
        Vector2[] span = catalog.Count == 0
            ? [new(TrackX, TrackTop)]
            : [new(TrackX, start), new(TrackX, start + length)];
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
        FitLabel(state.Names[row], name, NameSize);
        FitLabel(state.Songs[row], song, SongSize);
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

    private static void Place(Entity entity, Vector2 position)
    {
        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return;

        current.Value = position;
        entity.AddTag<Moved>();
    }

    private static void SetTextColor(Entity entity, Color color)
    {
        ref TextStyle style = ref entity.GetComponent<TextStyle>();
        if (style.Color == color)
            return;

        style.Color = color;
        entity.AddTag<Dirty>();
    }

    private static string MapsetName(LevelEntry? entry) =>
        entry is null
            ? ""
            : string.IsNullOrWhiteSpace(entry.Document.Name)
                ? Xanadu.Strings.LocalizedText.Get(Xanadu.Strings.SongSelectStrings.Untitled)
                : entry.Document.Name;

    private static string SongTitle(LevelEntry? entry) =>
        entry is null ? "" : entry.Song?.Title ?? "SONG MISSING";

    private static string Fit(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
