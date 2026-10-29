using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>Staggered level list with preview and player card panes.</summary>
public static partial class SongSelectScene
{

    /// <summary>Back, edit and play share the foot of the list panel.</summary>
    private const float ActionY = 1035;
    private static readonly Vector2 EmptyHintSize = new(360, 30);

    public static SongSelectState Build(
        EntityStore entities,
        Entity root,
        IReadOnlyList<LevelEntry> catalog,
        int selected,
        ProfileStore profiles,
        IVisualRidFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(
            entities, root, SceneUi.MenuLayer, visuals, UiStage.Full);
        bool loadAssets = visuals is GodotVisualRidFactory;

        Entity background = ui.Rect(SceneUi.BackgroundColor).Cover().Below().Entity;
        ApplyBackgroundShader(background, loadAssets);

        BuildChrome(ui, loadAssets);

        ui.Button(
                LocalizedText.Get(SongSelectStrings.Back),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(150, 44)
            .At(new Vector2(100, ActionY))
            .On(SceneKind.Main);

        (Entity level, Entity levelPanel) = BuildPlayerCard(ui, profiles, loadAssets);
        ScoreViews scores = BuildScores(ui, loadAssets);

        ListViews list = BuildList(ui, loadAssets);
        PreviewViews preview = BuildPreview(ui, loadAssets);

        InfoViews info = BuildInfo(ui, loadAssets);
        ActionViews actions = BuildActions(ui);

        var state = new SongSelectState(
            selected,
            catalog.Count,
            list.Tiles,
            list.Names,
            list.Songs,
            list.Thumb,
            preview.Chips,
            preview.Art,
            actions.Edit,
            list.EmptyTitle,
            list.EmptyHint);

        state.InfoDifficulty = info.Difficulty;
        state.InfoGenre = info.Genre;
        state.InfoTitle = info.Title;
        state.InfoArtist = info.Artist;
        state.ActionIcons = actions.Icons;
        state.LevelLabel = level;
        state.LevelPanel = levelPanel;
        state.ScoreViewport = scores.Viewport;
        state.ScoreContent = scores.Content;
        state.ScoreBars = scores.Bars;
        state.ScoreAccuracies = scores.Accuracies;
        state.ScoreValues = scores.Values;
        state.ScoreThumb = scores.Thumb;
        state.Scroll = Mathf.Clamp(selected, 0, Mathf.Max(catalog.Count - 1, 0));
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

        state.Catalog = catalog;
        RefreshList(ref state);
        RefreshChips(ref state, catalog);
        RefreshLevel(ref state);
        RefreshInfo(ref state, catalog);
        RefreshScores(ref state, catalog, profiles);
        RefreshEmptyState(ref state, catalog.Count == 0);

        bool blocked = catalog.Count > 0 &&
            catalog[state.Selected].IsBlocked;
        RefreshActions(ref state, !blocked);
    }

    /// <summary>
    /// With no levels the carousel has no rows to show, so it says why and
    /// offers a line that starts a new level in the editor.
    /// </summary>
    private static void RefreshEmptyState(ref SongSelectState state, bool empty)
    {
        SceneUi.SetText(
            state.EmptyTitle,
            empty ? LocalizedText.Get(SongSelectStrings.NoLevels) : "");
        SetVisible(
            state.EmptyHint,
            LocalizedText.Get(SongSelectStrings.CreateLevel),
            empty,
            EmptyHintSize);
    }

    private static void SetVisible(
        Entity button,
        string label,
        bool visible,
        Vector2 shownSize)
    {
        if (button.IsNull || !button.HasComponent<Rectangle>())
            return;

        ref Rectangle rect = ref button.GetComponent<Rectangle>();
        var size = visible ? shownSize : Vector2.Zero;
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
}
