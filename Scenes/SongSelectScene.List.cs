using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Scenes;

/// <summary>The staggered level list: scrolling rows around the selected level.</summary>
public static partial class SongSelectScene
{
    private const int RowCount = 8;

    /// <summary>The row holding the selected level when the list is at rest.</summary>
    private const int SelectedRow = 4;

    /// <summary>The visible slot, out of six, that the selection rests in.</summary>
    private const int SelectedSlot = 3;

    /// <summary>Exponential catch-up rate of the scroll, per second.</summary>
    public const float ScrollRate = 16f;
    private static readonly Vector2 RowSize = new(500, 80);
    private static readonly Color Transparent = new(0, 0, 0, 0);
    private static readonly Color RowColor = Colors.Black;
    private static readonly Color MapsetColor = new("#fbb03b");

    // Slots -1 and 6 sit just off either end and exist only for the slide.
    private static readonly float[] RowLeft =
        [14.4f, 52.138f, 89.859f, 122.696f, 149.198f, 104.308f, 80.21f, 60f];

    private static readonly float[] RowTop =
        [86.9f, 189.847f, 292.799f, 395.751f, 498.044f, 601.776f, 700.601f, 803.5f];

    private static readonly Vector2 NameOffset = TextCenter(15.24f, 40, 320, 40);
    private static readonly Vector2 SongOffset = TextCenter(15.24f, 68, 320, 30);
    private static readonly Vector2 ArtistOffset = TextCenter(338.74f, 36, 150, 18);
    private static readonly Vector2 DetailOffset = TextCenter(338.74f, 60, 150, 18);

    private const float TrackTop = 345.748f;
    private const float TrackBottom = 738.57f;
    private const float TrackX = 40.991f;
    private const float MinThumb = 40;

    private readonly record struct ListViews(
        List<Entity> Tiles,
        List<Entity> Names,
        List<Entity> Songs,
        List<Entity> Artists,
        List<Entity> Details,
        Entity Thumb);

    private static ListViews BuildList(UiBuilder ui)
    {
        var tiles = new List<Entity>();
        for (int row = 0; row < RowCount; row++)
        {
            Entity tile = ui.RectEntity(
                RowSize / 2,
                RowSize,
                RowColor,
                "",
                takesInput: true);
            tile.AddComponent(ButtonVisual.Create(RowColor));
            tile.AddComponent(new SongSelectTile(-1));
            tiles.Add(tile);
        }

        float left = RowLeft[SelectedSlot + 1];
        float top = RowTop[SelectedSlot + 1];
        Entity outline = ui.ShapeEntity(
            [
                new(left, top), new(left + RowSize.X, top),
                new(left + RowSize.X, top + RowSize.Y), new(left, top + RowSize.Y),
            ],
            Transparent,
            Amber,
            2);
        outline.AddTag<UiRaised>();
        ui.ShapeEntity(
            [new(86.396f, 540.628f), new(132.38f, 540.628f)],
            Transparent,
            Amber,
            4,
            closed: false);
        ui.ShapeEntity(
            [new(132.38f, 512.012f), new(132.38f, 569.307f)],
            Transparent,
            Amber,
            4,
            closed: false);

        var names = new List<Entity>();
        var songs = new List<Entity>();
        var artists = new List<Entity>();
        var details = new List<Entity>();
        for (int row = 0; row < RowCount; row++)
        {
            names.Add(RowText(ui, 320, 40, MapsetColor));
            songs.Add(RowText(ui, 320, 30, Colors.White));
            artists.Add(RowText(ui, 150, 18, Colors.White));
            details.Add(RowText(ui, 150, 18, Colors.White));
        }

        BuildScrollbar(ui, out Entity thumb);
        return new ListViews(tiles, names, songs, artists, details, thumb);
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
            MapsetColor,
            24,
            closed: false);
    }

    private static readonly Color ListBackdropTrack = new(0, 0, 0, 0.7f);

    /// <summary>Centre, relative to the row's corner, of text hung from a baseline.</summary>
    private static Vector2 TextCenter(float x, float baseline, float width, int fontSize) =>
        new(x + width / 2, baseline - fontSize * 0.33f);

    private static Entity RowText(UiBuilder ui, float width, int fontSize, Color color)
    {
        Entity text = SceneUi.Text(
            ui,
            Vector2.Zero,
            new Vector2(width, fontSize * 1.4f),
            "",
            fontSize,
            color,
            Transparent);
        text.AddComponent(new TextAlignment(TextAlign.Left));
        text.AddTag<UiRaised>();
        return text;
    }

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
            Place(state.Names[row], corner + NameOffset);
            Place(state.Songs[row], corner + SongOffset);
            Place(state.Artists[row], corner + ArtistOffset);
            Place(state.Details[row], corner + DetailOffset);

            SetTileColor(state.Tiles[row], Fade(RowColor, alpha));
            SetTextColor(state.Names[row], Fade(MapsetColor, alpha));
            SetTextColor(state.Songs[row], Fade(Colors.White, alpha));
            SetTextColor(state.Artists[row], Fade(Colors.White, alpha));
            SetTextColor(state.Details[row], Fade(Colors.White, alpha));
        }

        float length = Mathf.Clamp(
            (TrackBottom - TrackTop) * 6 / Mathf.Max(catalog.Count, 1),
            MinThumb,
            TrackBottom - TrackTop);
        float travel = TrackBottom - TrackTop - length;
        float t = catalog.Count > 1
            ? Mathf.Clamp(state.Scroll / (catalog.Count - 1), 0, 1)
            : 0;
        float start = TrackTop + travel * t;
        ref UiShape thumb = ref state.Thumb.GetComponent<UiShape>();
        thumb.Points = [new(TrackX, start), new(TrackX, start + length)];
        state.Thumb.AddTag<Dirty>();
    }

    private static void BindRow(ref SongSelectState state, int row, LevelEntry? entry)
    {
        SceneUi.SetText(state.Names[row], Fit(MapsetName(entry), 18));
        SceneUi.SetText(state.Songs[row], Fit(SongTitle(entry), 20));
        SceneUi.SetText(state.Artists[row], Fit(entry?.Song?.Artist ?? "", 16));
        SceneUi.SetText(
            state.Details[row],
            entry is null ? "" : $"{entry.Document.Notes.Count} notes");
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
        slot < 0 ? Mathf.Clamp(slot + 1, 0, 1) : Mathf.Clamp(6 - slot, 0, 1);

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
