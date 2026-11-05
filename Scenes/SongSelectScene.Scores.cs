using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Scenes;

/// <summary>
/// The scores panel: every profile's best on the selected level, highest
/// first, as a black bar each showing accuracy then score. Bars exist only for
/// scores that exist. The list scrolls with the wheel and its scrollbar follows.
/// </summary>
public static partial class SongSelectScene
{
    /// <summary>Bars the viewport shows before the list starts to scroll.</summary>
    private const int VisibleScoreBars = 4;

    private const float BarHeight = 50;
    private const float BarGap = 8;
    private const float BarInset = 20;
    /// <summary>Half the original panel, pinned to the same right edge as before.</summary>
    private const float PanelWidth = 552.37f;
    private const float PanelRight = 1894.334f;

    /// <summary>The panel's width less its padding, the gap before the scrollbar and the scrollbar itself.</summary>
    private const float ScoreViewWidth = 486.75f;
    private const float ScoreViewHeight = VisibleScoreBars * BarHeight + (VisibleScoreBars - 1) * BarGap;
    private const float ScoreAccuracyWidth = 150;

    private const float BarTrackLeft = 1863.358f;
    private const float BarTrackRight = 1877.122f;
    private const float BarTrackTop = 726.929f;
    private const float BarTrackBottom = 1036.348f;
    private const float MinScoreThumb = 24;
    private const float BarHitMargin = 6;

    private readonly record struct ScoreViews(
        Entity Viewport,
        UiNode Content,
        Entity Thumb,
        Entity Track);

    /// <summary>
    /// The panel is a container: the tab, then a clipped viewport holding a
    /// stack that <see cref="RefreshScores"/> fills with a row container per
    /// score, each laying its accuracy and score side by side. Only the scrollbar, which sits outside the stack, is placed by hand.
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

        viewport.With(new ScrollView());

        ui.Shape(
            RoundedRect(BarTrackLeft, BarTrackTop, BarTrackRight, BarTrackBottom, 3.2f),
            Dim);
        Entity thumb = ui.Shape(
                RoundedRect(BarTrackLeft, BarTrackTop, BarTrackRight, BarTrackBottom, 3.2f),
                Gold)
            .With(new ShapeGlow(new Color(Gold, 0.6f), radius: 10, intensity: 1.5f))
            .Raised();

        // Invisible and a little wider than the thin bar: what the pointer actually presses.
        Entity track = ui.Rect(
                Transparent,
                "",
                new Vector2(BarTrackRight - BarTrackLeft + BarHitMargin * 2, BarTrackBottom - BarTrackTop))
            .At(new Vector2((BarTrackLeft + BarTrackRight) / 2, (BarTrackTop + BarTrackBottom) / 2))
            .Input()
            .Raised();

        return new ScoreViews(
            viewport.Entity,
            content,
            thumb,
            track);
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
    /// Fills the rows with the selected level's scores, adding and deleting rows
    /// to match, so the stack measures only real scores and the scroll range follows.
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

        if (state.ScoresFor != path)
        {
            state.ScoresFor = path;
            state.ScoreViewport.GetComponent<ScrollView>().Offset = 0;
        }

        Entity rows = state.ScoreContent.Entity;
        while (rows.ChildCount < scores.Count)
            AddScoreRow(state.ScoreContent);
        while (rows.ChildCount > scores.Count)
            UiLayout.DeleteSubtree(rows.ChildEntities[rows.ChildCount - 1]);

        for (int i = 0; i < scores.Count; i++)
        {
            ChildEntities cells = rows.ChildEntities[i].ChildEntities;
            SceneUi.SetText(cells[0], $"{scores[i].Record.Accuracy * 100:0.00}%");
            SceneUi.SetText(cells[1], $"{scores[i].Record.Score:0000000}");
        }
    }

    /// <summary>One black bar holding the accuracy, then the score.</summary>
    private static void AddScoreRow(UiNode rows)
    {
        UiNode bar = rows.Row()
            .Size(ScoreViewWidth, BarHeight)
            .Fill(PanelBlack)
            .Pad(BarInset)
            .Main(StackMainAlignment.Start);
        ScoreText(bar, ScoreAccuracyWidth, TextAlign.Left, GoldLight);
        ScoreText(
            bar,
            ScoreViewWidth - BarInset * 2 - ScoreAccuracyWidth,
            TextAlign.Right,
            Colors.White);
    }

    /// <summary>How much the score rows overflow their viewport.</summary>
    private static float ScoreRange(in SongSelectState state) =>
        Mathf.Max(0, state.ScoreContent.Entity.GetComponent<Rectangle>().Bounds.Size.Y - ScoreViewHeight);

    /// <summary>The scores' scrollbar; without overflow the thumb fills the track.</summary>
    public static VerticalSlider ScoreSlider(in SongSelectState state)
    {
        float content = state.ScoreContent.Entity.GetComponent<Rectangle>().Bounds.Size.Y;
        return new VerticalSlider(
            BarTrackTop,
            BarTrackBottom,
            ScoreRange(state) <= 0 ? 1f : ScoreViewHeight / content,
            MinScoreThumb);
    }

    /// <summary>How far down the scores are scrolled, from 0 to 1.</summary>
    public static float ScoreValue(in SongSelectState state)
    {
        float range = ScoreRange(state);
        return range <= 0 ? 0 : state.ScoreViewport.GetComponent<ScrollView>().Offset / range;
    }

    /// <summary>Scrolls the scores to a point from 0 at the top to 1 at the bottom.</summary>
    public static void SetScoreValue(in SongSelectState state, float value) =>
        state.ScoreViewport.GetComponent<ScrollView>().Offset = value * ScoreRange(state);

    /// <summary>Sizes and places the scrollbar thumb for how far the scores are scrolled.</summary>
    public static void RefreshScoreThumb(ref SongSelectState state)
    {
        if (state.ScoreViewport.IsNull)
            return;

        VerticalSlider slider = ScoreSlider(state);
        float length = slider.ThumbLength;
        float start = slider.ThumbTop(ScoreValue(state));

        var span = new Vector2(start, length);
        if (state.ScoreThumbSpan == span)
            return;

        state.ScoreThumbSpan = span;
        SetPoints(
            state.ScoreThumb,
            RoundedRect(BarTrackLeft, start, BarTrackRight, start + length, 3.2f));
    }
}
