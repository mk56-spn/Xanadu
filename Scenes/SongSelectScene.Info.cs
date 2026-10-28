using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Scenes;

/// <summary>
/// The selected level's card: its difficulty's name, then the song's genre,
/// title and artist, stacked down a skewed panel so each line follows the slant.
/// </summary>
public static partial class SongSelectScene
{
    private const float InfoLeft = 1345;
    private const float InfoWidth = 600;
    private const float InfoTop = 262;
    private const float InfoPadding = 14;
    private const float InfoTextWidth = 540;
    private const int TitleLargest = 46;
    private const int TitleSmallest = 24;

    private readonly record struct InfoViews(Entity Difficulty, Entity Genre, Entity Title, Entity Artist);

    private static InfoViews BuildInfo(UiBuilder ui, bool loadAssets)
    {
        const float differenceHeight = 44;
        const float genreHeight = 30;
        const float titleHeight = 60;
        const float artistHeight = 36;
        const float gap = 6;
        float height = differenceHeight + genreHeight + titleHeight + artistHeight +
                       gap * 3 + InfoPadding * 2;

        // The card's left edge runs down the same slant as everything else, and
        // its right end runs off screen.
        ui.Container(
            new Vector2(InfoLeft + InfoWidth / 2, InfoTop + height / 2),
            new Vector2(InfoWidth, height),
            StackDirection.Vertical,
            spacing: gap,
            color: Dim,
            padding: InfoPadding,
            cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start,
            skew: PreviewTilt);
        Frost(ui.Current, loadAssets);

        // The difficulty's name sits on a small skewed plate in the chip colour.
        ui.Container(
            Vector2.Zero,
            new Vector2(300, differenceHeight),
            StackDirection.Vertical,
            color: Transparent,
            text: "",
            skew: PreviewTilt);
        Entity difficulty = ui.Current;
        difficulty.AddComponent(new TextStyle(30, Colors.White, bold: true));
        difficulty.AddComponent(new UiBorder(Transparent, 3));
        ui.End();

        Entity genre = InfoLine(ui, genreHeight, 22, Gold);
        Entity title = InfoLine(ui, titleHeight, TitleLargest, Colors.White);
        Entity artist = InfoLine(ui, artistHeight, 28, new Color(0.85f, 0.87f, 0.9f));
        ui.End();

        return new InfoViews(difficulty, genre, title, artist);
    }

    private static Entity InfoLine(UiBuilder ui, float height, int fontSize, Color color)
    {
        Entity line = SceneUi.Text(
            ui,
            Vector2.Zero,
            new Vector2(InfoTextWidth, height),
            "",
            fontSize,
            color,
            Transparent);
        line.AddComponent(new TextAlignment(TextAlign.Left));
        line.GetComponent<TextStyle>().Bold = true;
        return line;
    }

    private static void RefreshInfo(ref SongSelectState state, IReadOnlyList<LevelEntry> catalog)
    {
        LevelEntry? entry = catalog.Count == 0 ? null : catalog[state.Selected];
        // A level's name is its difficulty's name; the editor calls the same field Difficulty.
        string difficulty = entry is null ? "" : MapsetName(entry).Trim().ToUpperInvariant();
        string genre = entry?.Song?.Genre?.Trim().ToUpperInvariant() ?? "";
        string title = entry is null ? "" : SongTitle(entry);
        string artist = entry?.Song?.Artist ?? "";

        SceneUi.SetText(state.InfoDifficulty, difficulty);
        Color plate = difficulty.Length == 0 ? Transparent : ChipStroke[0];
        Color edge = difficulty.Length == 0 ? Transparent : ChipFill[0];
        ref FillColor fill = ref state.InfoDifficulty.GetComponent<FillColor>();
        ref UiBorder border = ref state.InfoDifficulty.GetComponent<UiBorder>();
        if (fill.Value != plate || border.Color != edge)
        {
            fill.Value = plate;
            border.Color = edge;
            state.InfoDifficulty.AddTag<Dirty>();
        }

        SceneUi.SetText(state.InfoGenre, genre);
        SceneUi.SetText(state.InfoArtist, artist);
        SceneUi.SetText(state.InfoTitle, title);

        // Long titles shrink to stay inside the card rather than being clipped.
        int size = Mathf.Clamp(
            Mathf.FloorToInt(InfoTextWidth / (Mathf.Max(title.Length, 1) * 0.56f)),
            TitleSmallest,
            TitleLargest);
        ref TextStyle style = ref state.InfoTitle.GetComponent<TextStyle>();
        if (style.FontSize != size)
        {
            style.FontSize = size;
            state.InfoTitle.AddTag<Dirty>();
        }
    }
}
