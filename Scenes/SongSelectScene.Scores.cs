using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Strings;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Scenes;

/// <summary>
/// The scores panel: every profile's best on the selected level, highest
/// first, as a black bar each showing accuracy then score. The list scrolls
/// with the wheel and its scrollbar follows.
/// </summary>
public static partial class SongSelectScene
{
    /// <summary>Bars always shown, filled or not, as in the design.</summary>
    private const int MinScoreBars = 4;

    /// <summary>Most scores one level can list; further profiles are not shown.</summary>
    private const int MaxScoreBars = 30;

    private const float BarHeight = 50;
    private const float BarGap = 8;
    private const float BarInset = 20;
    /// <summary>Half the original panel, pinned to the same right edge as before.</summary>
    private const float PanelWidth = 552.37f;
    private const float PanelRight = 1894.334f;

    /// <summary>The panel's width less its padding, the gap before the scrollbar and the scrollbar itself.</summary>
    private const float ScoreViewWidth = 486.75f;
    private const float ScoreViewHeight = MinScoreBars * BarHeight + (MinScoreBars - 1) * BarGap;
    private const float ScoreAccuracyWidth = 150;

    private const float BarTrackLeft = 1863.358f;
    private const float BarTrackRight = 1877.122f;
    private const float BarTrackTop = 726.929f;
    private const float BarTrackBottom = 1036.348f;
    private const float MinScoreThumb = 24;

    private readonly record struct ScoreViews(
        Entity Viewport,
        Entity Content,
        List<Entity> Bars,
        List<Entity> Accuracies,
        List<Entity> Values,
        Entity Thumb);

    /// <summary>
    /// The panel is a container: the tab, then a clipped viewport holding a
    /// stack of row containers, each laying its accuracy and score side by
    /// side. Only the scrollbar, which sits outside the stack, is placed by hand.
    /// </summary>
    private static ScoreViews BuildScores(UiNode ui, bool loadAssets)
    {
        const float padding = 16.33f;

        UiNode panel = ui.Column()
            .Size(PanelWidth, 375.369f)
            .At(new Vector2(PanelRight - PanelWidth / 2, 871.275f))
            .Gap(12.43f)
            .Fill(Dim)
            .Pad(padding)
            .Cross(StackCrossAlignment.Start)
            .Main(StackMainAlignment.Start);
        Frost(panel.Entity, loadAssets);

        panel.Text(
                LocalizedText.Get(SongSelectStrings.Scores),
                35,
                Gold,
                TextAlign.Center)
            .Size(159.293f, 60.113f)
            .Fill(Dim)
            .Bold()
            .Glow(new Color(Gold, 0.6f), radius: 10, intensity: 1.5f);

        UiNode viewport = panel.Column()
            .Size(ScoreViewWidth, ScoreViewHeight)
            .Clip()
            .Input()
            .Fill(Transparent);

        UiNode content = viewport.Column()
            .Size(ScoreViewWidth, ScoreViewHeight)
            .Gap(BarGap)
            .AutoHeight()
            .Main(StackMainAlignment.Start)
            .Fill(Transparent);

        var bars = new List<Entity>();
        var accuracies = new List<Entity>();
        var values = new List<Entity>();
        for (int i = 0; i < MaxScoreBars; i++)
        {
            UiNode bar = content.Row()
                .Size(ScoreViewWidth, BarHeight)
                .Fill(Transparent)
                .Pad(BarInset)
                .Main(StackMainAlignment.Start);
            bars.Add(bar.Entity);

            accuracies.Add(ScoreText(
                bar, ScoreAccuracyWidth, TextAlign.Left, GoldLight).Entity);
            values.Add(ScoreText(
                bar,
                ScoreViewWidth - BarInset * 2 - ScoreAccuracyWidth,
                TextAlign.Right,
                Colors.White).Entity);
        }

        viewport.With(new ScrollView(content.Entity));

        ui.Shape(
            RoundedRect(BarTrackLeft, BarTrackTop, BarTrackRight, BarTrackBottom, 3.2f),
            Dim);
        Entity thumb = ui.Shape(
                RoundedRect(BarTrackLeft, BarTrackTop, BarTrackRight, BarTrackBottom, 3.2f),
                Gold)
            .With(new ShapeGlow(new Color(Gold, 0.6f), radius: 10, intensity: 1.5f))
            .Raised();

        return new ScoreViews(
            viewport.Entity,
            content.Entity,
            bars,
            accuracies,
            values,
            thumb);
    }

    private static UiNode ScoreText(
        UiNode bar,
        float width,
        TextAlign align,
        Color color) =>
        bar.Rect(Transparent, "", new Vector2(width, BarHeight))
            .With(new TextStyle(28, color, bold: true))
            .Align(align);

    /// <summary>
    /// Fills the rows with the selected level's scores. Unused rows drop out of
    /// the stack, so it measures only the bars in use and the scroll range follows.
    /// </summary>
    private static void RefreshScores(
        ref SongSelectState state,
        IReadOnlyList<LevelEntry> catalog,
        ProfileStore profiles)
    {
        string path = catalog.Count == 0 ? "" : catalog[state.Selected].Path;
        IReadOnlyList<ScoreEntry> scores = path.Length == 0
            ? []
            : profiles.AllScores(path);

        int shown = Mathf.Min(scores.Count, MaxScoreBars);
        int bars = Mathf.Clamp(shown, MinScoreBars, MaxScoreBars);

        if (state.ScoresFor != path)
        {
            state.ScoresFor = path;
            state.ScoreViewport.GetComponent<ScrollView>().Offset = 0;
        }

        bool restack = false;
        for (int i = 0; i < MaxScoreBars; i++)
        {
            Entity bar = state.ScoreBars[i];
            bool used = i < bars;
            if (bar.Tags.Has<LayoutIgnore>() == used)
            {
                if (used)
                    bar.RemoveTag<LayoutIgnore>();
                else
                    bar.AddTag<LayoutIgnore>();

                restack = true;
            }

            SetTileColor(bar, used ? PanelBlack : Transparent);
            bool filled = i < shown;
            SceneUi.SetText(
                state.ScoreAccuracies[i],
                filled ? $"{scores[i].Record.Accuracy * 100:0.00}%" : "");
            SceneUi.SetText(
                state.ScoreValues[i],
                filled ? $"{scores[i].Record.Score:0000000}" : "");
        }

        if (restack)
            state.ScoreContent.AddTag<LayoutDirty>();
    }

    /// <summary>Sizes and places the scrollbar thumb for how far the scores are scrolled.</summary>
    public static void RefreshScoreThumb(ref SongSelectState state)
    {
        if (state.ScoreViewport.IsNull)
            return;

        float track = BarTrackBottom - BarTrackTop;
        float content = state.ScoreContent.GetComponent<Rectangle>().Bounds.Size.Y;
        float range = Mathf.Max(0, content - ScoreViewHeight);
        float length = range <= 0
            ? track
            : Mathf.Clamp(track * ScoreViewHeight / content, MinScoreThumb, track);
        float offset = Mathf.Clamp(state.ScoreViewport.GetComponent<ScrollView>().Offset, 0, range);
        float start = BarTrackTop + (range <= 0 ? 0 : (track - length) * offset / range);

        var span = new Vector2(start, length);
        if (state.ScoreThumbSpan == span)
            return;

        state.ScoreThumbSpan = span;
        SetPoints(
            state.ScoreThumb,
            RoundedRect(BarTrackLeft, start, BarTrackRight, start + length, 3.2f));
    }
}
