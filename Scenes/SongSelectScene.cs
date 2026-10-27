using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Levels;
using Xanadu.Profiles;

namespace Xanadu.Scenes;

/// <summary>Three-by-three level grid with an empty details pane.</summary>
public static class SongSelectScene
{
    private const int Columns = 3;
    private const int Rows = 3;
    private const int PageSize = Columns * Rows;

    public static SongSelectState Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        IReadOnlyList<LevelEntry> catalog,
        int selected,
        ProfileStore profiles,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 48),
            new Vector2(900, 58),
            LocalizedText.Get(SongSelectStrings.Title),
            40,
            SceneUi.AccentColor);

        SceneUi.Button(
            ui,
            new Vector2(100, 44),
            new Vector2(150, 44),
            LocalizedText.Get(SongSelectStrings.Back),
            SceneKind.Main,
            18,
            SceneUi.MutedButtonColor);

        float leftWidth = screen.X * 0.46f;
        SceneUi.Panel(
            ui,
            new Vector2(leftWidth / 2 + 8, screen.Y / 2 + 20),
            new Vector2(leftWidth - 32, screen.Y - 170));

        Vector2 profileBox = new(screen.X - 154, 44);
        ui.RectEntity(profileBox, new Vector2(260, 64), SceneUi.PanelColor);
        Texture2D? star = visuals is GodotVisualRidFactory
            ? LoadStarTexture()
            : null;
        if (star is not null)
        {
            ui.ImageEntity(
                profileBox + new Vector2(-92, 0),
                new Vector2(36, 36),
                star);
        }
        else
        {
            SceneUi.Text(
                ui,
                profileBox + new Vector2(-92, 0),
                new Vector2(36, 36),
                "★",
                26,
                SceneUi.AccentColor,
                SceneUi.PanelColor);
        }

        SceneUi.Text(
            ui,
            profileBox + new Vector2(24, 0),
            new Vector2(170, 24),
            profiles.ActiveProfile?.Name ??
                LocalizedText.Get(SongSelectStrings.NoProfile),
            16,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        float gridLeft = leftWidth + 24;
        float gridRight = screen.X - 24;
        float gridTop = 96;
        float gridBottom = screen.Y - 132;
        const float gap = 12;
        float tileWidth = (gridRight - gridLeft - gap * (Columns - 1)) / Columns;
        float tileHeight = (gridBottom - gridTop - gap * (Rows - 1)) / Rows;
        var tiles = new List<Entity>();
        var names = new List<Entity>();
        var metas = new List<Entity>();

        for (int slot = 0; slot < PageSize; slot++)
        {
            int column = slot % Columns;
            int row = slot / Columns;
            Vector2 center = new(
                gridLeft + column * (tileWidth + gap) + tileWidth / 2,
                gridTop + row * (tileHeight + gap) + tileHeight / 2);

            Entity tile = ui.RectEntity(
                center,
                new Vector2(tileWidth, tileHeight),
                SceneUi.PanelColor,
                "",
                takesInput: true);
            tile.AddComponent(ButtonVisual.Create(SceneUi.PanelColor));
            tile.AddComponent(new SongSelectTile(slot));

            Entity name = SceneUi.Text(
                ui,
                center + new Vector2(0, -tileHeight * 0.18f),
                new Vector2(tileWidth - 16, 24),
                "",
                16,
                SceneUi.TextColor,
                new Color(0, 0, 0, 0));
            Entity meta = SceneUi.Text(
                ui,
                center + new Vector2(0, tileHeight * 0.16f),
                new Vector2(tileWidth - 16, 20),
                "",
                12,
                SceneUi.DimTextColor,
                new Color(0, 0, 0, 0));

            tiles.Add(tile);
            names.Add(name);
            metas.Add(meta);
        }

        float actionY = screen.Y - 74;
        Entity edit = SceneUi.ButtonEntity(
            ui,
            new Vector2((gridLeft + gridRight) / 2 - 100, actionY),
            new Vector2(180, 48),
            LocalizedText.Get(SongSelectStrings.EditLevel),
            16,
            SceneUi.MutedButtonColor);
        edit.AddComponent(new SceneCommand(SceneActionId.Edit));

        Entity play = SceneUi.ButtonEntity(
            ui,
            new Vector2((gridLeft + gridRight) / 2 + 100, actionY),
            new Vector2(180, 48),
            LocalizedText.Get(SongSelectStrings.Play),
            20);
        play.AddComponent(new SceneCommand(SceneActionId.Play));

        var state = new SongSelectState(
            selected,
            catalog.Count,
            tiles,
            names,
            metas,
            play,
            edit,
            LocalizedText.Get(SongSelectStrings.Play),
            LocalizedText.Get(SongSelectStrings.EditLevel));

        Refresh(ref state, catalog, selected, profiles);
        return state;
    }

    public static void Refresh(
        ref SongSelectState state,
        IReadOnlyList<LevelEntry> catalog,
        int selected,
        ProfileStore profiles)
    {
        state.Count = catalog.Count;
        state.Selected = catalog.Count == 0
            ? 0
            : Mathf.Clamp(selected, 0, catalog.Count - 1);
        state.PageStart = state.Selected / PageSize * PageSize;

        for (int slot = 0; slot < state.Tiles.Count; slot++)
        {
            int index = state.PageStart + slot;
            bool valid = index < catalog.Count;
            Entity tile = state.Tiles[slot];

            if (tile.HasComponent<SongSelectTile>())
                tile.GetComponent<SongSelectTile>().Index = index;

            if (!valid)
            {
                SceneUi.SetText(state.Names[slot], "");
                SceneUi.SetText(state.Metas[slot], "");
                SetTileColor(tile, new Color("#111827"));
                continue;
            }

            LevelEntry entry = catalog[index];
            SceneUi.SetText(
                state.Names[slot],
                string.IsNullOrWhiteSpace(entry.Document.Name)
                    ? LocalizedText.Get(SongSelectStrings.Untitled)
                    : entry.Document.Name);
            SceneUi.SetText(
                state.Metas[slot],
                entry.Song is null
                    ? "SONG MISSING"
                    : $"{entry.Song.Title}   ·   {entry.Document.Notes.Count} notes");
            SetTileColor(
                tile,
                index == state.Selected
                    ? SceneUi.AccentColor
                    : entry.IsBlocked
                        ? new Color("#4a2222")
                        : SceneUi.PanelColor);
        }

        bool blocked = catalog.Count > 0 &&
            catalog[state.Selected].IsBlocked;
        SetActionVisible(state.Play, state.PlayLabel, !blocked);
        SetActionVisible(state.Edit, state.EditLabel, !blocked);
    }

    private static void SetActionVisible(
        Entity button,
        string label,
        bool visible)
    {
        if (button.IsNull || !button.HasComponent<Rectangle>())
            return;

        ref Rectangle rect = ref button.GetComponent<Rectangle>();
        var size = visible ? new Vector2(180, 48) : Vector2.Zero;
        if (rect.Bounds.Size == size)
            return;

        rect.Bounds = new Rect2(-size / 2, size);
        if (button.HasComponent<Text>())
            button.GetComponent<Text>().Value = visible ? label : "";
        button.AddTag<Dirty>();
        if (visible && button.HasComponent<ButtonVisual>())
        {
            ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
            visual.HoverAmount = 0f;
            visual.FlashAmount = 0f;
        }
    }

    private static void SetTileColor(Entity tile, Color color)
    {
        if (tile.IsNull || !tile.HasComponent<FillColor>())
            return;

        ref FillColor fill = ref tile.GetComponent<FillColor>();
        if (fill.Value == color)
            return;

        fill.Value = color;
        if (tile.HasComponent<ButtonVisual>())
        {
            ref ButtonVisual visual = ref tile.GetComponent<ButtonVisual>();
            visual.Normal = color;
            visual.Hover = color.Lerp(Colors.White, 0.16f);
        }
        tile.AddTag<Dirty>();
    }

    private static Texture2D? LoadStarTexture()
    {
        try
        {
            string svg = FileAccess.GetFileAsString("res://Assets/star.svg");
            if (string.IsNullOrEmpty(svg))
                return null;

            var image = new Image();
            image.LoadSvgFromString(svg, 2f);
            return ImageTexture.CreateFromImage(image);
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
