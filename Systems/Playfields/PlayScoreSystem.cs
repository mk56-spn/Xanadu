using System;
using Friflo.Engine.ECS;
using Godot;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Strings;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Projects score, accuracy, rank, judgement, and combo into the play HUD.</summary>
public sealed class PlayScoreSystem : QuerySystem<ScoreState, Playhead>
{
    /// <summary>How far under the judgement's centre the EARLY / LATE label sits.</summary>
    public const float TimingIndicatorGap = 30f;

    public static Color ColorFor(Judgement judgement) => judgement switch
    {
        Judgement.FlawlessP => Colors.MediumPurple.Lightened(0.3f),
        Judgement.Flawless => Colors.MediumPurple,
        Judgement.Clean => Colors.Cyan,
        Judgement.Fair => Colors.GreenYellow,
        Judgement.Deficient => Colors.Orange,
        Judgement.Terrible => Colors.DarkOrange.Darkened(0.3f),
        Judgement.Miss => Colors.DarkRed,
        Judgement.Mine => Palette.DangerBright,
        Judgement.Contact => Palette.Warning,
        _ => Colors.White,
    };

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref score, ref playhead, entity) =>
        {
            UiText.Set(score.ScoreText, $"SCORE {score.Score:0000000}", buffer);
            UiText.Set(
                score.AccuracyText,
                $"ACC {score.Accuracy * 100:0.00}%  {ScoreRules.RankFor(score.Accuracy)}",
                buffer);
            bool showJudgement =
                score.HasJudgement &&
                playhead.TimeMs >= score.JudgementTimeMs &&
                playhead.TimeMs - score.JudgementTimeMs <= 700;

            UiText.Set(
                score.JudgementText,
                showJudgement
                    ? ScoreRules.LabelFor(score.LastJudgement)
                    : "",
                buffer);

            if (showJudgement &&
                !score.JudgementText.IsNull &&
                score.JudgementText.HasComponent<TextStyle>())
            {
                ref TextStyle style =
                    ref score.JudgementText.GetComponent<TextStyle>();
                Color color = ColorFor(score.LastJudgement);
                if (style.Color != color)
                {
                    style.Color = color;
                    buffer.AddTag<Dirty>(score.JudgementText.Id);
                }
            }

            if (entity.HasComponent<ErrorBar>())
                UpdateErrorBar(entity, score, showJudgement, buffer);

            if (entity.TryGetComponent(out TimingIndicator timing))
                UpdateTimingIndicator(entity, timing, score, showJudgement, buffer);

            if (entity.HasComponent<JudgementCounter>())
            {
                JudgementCounter counter =
                    entity.GetComponent<JudgementCounter>();
                UiText.Set(counter.FlawlessPlus,
                    score.FlawlessPCount.ToString(), buffer);
                UiText.Set(counter.Flawless,
                    score.FlawlessCount.ToString(), buffer);
                UiText.Set(counter.Clean,
                    score.CleanCount.ToString(), buffer);
                UiText.Set(counter.Fair,
                    score.FairCount.ToString(), buffer);
                UiText.Set(counter.Deficient,
                    score.DeficientCount.ToString(), buffer);
                UiText.Set(counter.Terrible,
                    score.TerribleCount.ToString(), buffer);
                UiText.Set(counter.Miss,
                    score.MissCount.ToString(), buffer);
            }

            if (showJudgement &&
                !score.JudgementText.IsNull &&
                score.JudgementText.HasComponent<Position>())
            {
                // The judgement belongs to the note that was hit, so it always
                // sits on it; it is not a HUD element that can be moved away.
                ref Position position =
                    ref score.JudgementText.GetComponent<Position>();
                position.Value = JudgementScreenPosition(entity, score);
                buffer.AddTag<Moved>(score.JudgementText.Id);
            }
        });
    }

    private static Vector2 JudgementScreenPosition(Entity entity, ScoreState score) =>
        entity.TryGetComponent(out PlayCamera camera)
            ? score.JudgementPosition - camera.Shift
            : score.JudgementPosition;

    private static void UpdateTimingIndicator(
        Entity entity,
        TimingIndicator indicator,
        ScoreState score,
        bool visible,
        CommandBuffer buffer)
    {
        HitTiming timing = visible
            ? ScoreRules.TimingFor(score.LastJudgement, score.LastOffsetMs)
            : HitTiming.None;

        UiLayout.SetHidden(indicator.Text, buffer, timing == HitTiming.None);
        if (timing == HitTiming.None)
            return;

        UiText.Set(
            indicator.Text,
            LocalizedText.Get(timing == HitTiming.Early
                ? PlayStrings.Early
                : PlayStrings.Late),
            buffer);

        ref TextStyle style = ref indicator.Text.GetComponent<TextStyle>();
        Color color = timing == HitTiming.Early ? Palette.Info : Palette.Warning;
        if (style.Color != color)
        {
            style.Color = color;
            buffer.AddTag<Dirty>(indicator.Text.Id);
        }

        ref Position position = ref indicator.Text.GetComponent<Position>();
        position.Value = JudgementScreenPosition(entity, score) +
                         new Vector2(0, TimingIndicatorGap);
        buffer.AddTag<Moved>(indicator.Text.Id);
    }

    private static void UpdateErrorBar(
        Entity entity,
        ScoreState score,
        bool visible,
        CommandBuffer buffer)
    {
        ErrorBar bar = entity.GetComponent<ErrorBar>();
        if (bar.Marker.IsNull ||
            !bar.Marker.HasComponent<Position>() ||
            !bar.Marker.HasComponent<FillColor>())
        {
            return;
        }

        bool show = visible &&
                    score.LastJudgement != Judgement.Miss &&
                    score.LastJudgement != Judgement.Mine &&
                    score.LastJudgement != Judgement.Contact &&
                    score.LastJudgement != Judgement.None;
        Color color = show
            ? ColorFor(score.LastJudgement)
            : new Color(1, 1, 1, 0);
        color.A = show ? 1f : 0f;

        if (show)
        {
            float offset = Math.Clamp(
                score.LastOffsetMs,
                -(long)bar.MaxOffsetMs,
                (long)bar.MaxOffsetMs);
            ref Position position = ref bar.Marker.GetComponent<Position>();
            position.Value = new Vector2(
                bar.CenterX - offset / bar.MaxOffsetMs * bar.HalfWidth,
                bar.Y);
            buffer.AddTag<Moved>(bar.Marker.Id);
        }

        ref FillColor fill = ref bar.Marker.GetComponent<FillColor>();
        if (fill.Value != color)
        {
            fill.Value = color;
            buffer.AddTag<Dirty>(bar.Marker.Id);
        }
    }

    private static void Count(ref ScoreState score, Judgement judgement) =>
        PlayScoring.Count(ref score, judgement);
}
