using System;
using Friflo.Engine.ECS;
using Godot;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Projects score, accuracy, rank, judgement, and combo into the play HUD.</summary>
public sealed class PlayScoreSystem : QuerySystem<ScoreState, Playhead>
{
    public static Color ColorFor(Judgement judgement) => judgement switch
    {
        Judgement.FlawlessP => Palette.Accent,
        Judgement.Flawless => Palette.Cyan,
        Judgement.Clean => Palette.SuccessBright,
        Judgement.Fair => Palette.InfoSoft,
        Judgement.Deficient => Palette.Warning,
        Judgement.Terrible => Palette.Danger,
        Judgement.Miss => Palette.TextDim,
        Judgement.Mine => Palette.DangerBright,
        _ => Colors.White,
    };

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref score, ref playhead, entity) =>
        {
            UiText.Set(score.ScoreText, $"SCORE {score.Score:0000000}", buffer);
            UiText.Set(score.ComboText, $"COMBO {score.Combo}", buffer);
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
                position.Value = score.JudgementPosition;
                buffer.AddTag<Moved>(score.JudgementText.Id);
            }
        });
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
