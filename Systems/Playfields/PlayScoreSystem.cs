using System;
using Friflo.Engine.ECS;
using Godot;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Scoring;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Projects score, accuracy, rank, judgement, and combo into the play HUD.</summary>
public sealed class PlayScoreSystem : QuerySystem<ScoreState, Playhead>
{
    public static Color ColorFor(Judgement judgement) => judgement switch
    {
        Judgement.FlawlessP => new("#facc15"),
        Judgement.Flawless => new("#22d3ee"),
        Judgement.Clean => new("#4ade80"),
        Judgement.Fair => new("#60a5fa"),
        Judgement.Deficient => new("#fb923c"),
        Judgement.Terrible => new("#ef4444"),
        Judgement.Miss => new("#94a3b8"),
        Judgement.Mine => new("#ff3b30"),
        _ => Colors.White,
    };

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref score, ref playhead, entity) =>
        {
            SetText(score.ScoreText, $"SCORE {score.Score:0000000}", buffer);
            SetText(score.ComboText, $"COMBO {score.Combo}", buffer);
            SetText(
                score.AccuracyText,
                $"ACC {score.Accuracy * 100:0.00}%  {ScoreRules.RankFor(score.Accuracy)}",
                buffer);
            bool showJudgement =
                score.HasJudgement &&
                playhead.TimeMs >= score.JudgementTimeMs &&
                playhead.TimeMs - score.JudgementTimeMs <= 700;

            SetText(
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
                SetText(counter.FlawlessPlus,
                    score.FlawlessPCount.ToString(), buffer);
                SetText(counter.Flawless,
                    score.FlawlessCount.ToString(), buffer);
                SetText(counter.Clean,
                    score.CleanCount.ToString(), buffer);
                SetText(counter.Fair,
                    score.FairCount.ToString(), buffer);
                SetText(counter.Deficient,
                    score.DeficientCount.ToString(), buffer);
                SetText(counter.Terrible,
                    score.TerribleCount.ToString(), buffer);
                SetText(counter.Miss,
                    score.MissCount.ToString(), buffer);
            }

            if (showJudgement &&
                !score.JudgementText.IsNull &&
                score.JudgementText.HasComponent<Position>())
            {
                ref Position position =
                    ref score.JudgementText.GetComponent<Position>();
                position.Value = score.JudgementPosition + score.HudOffset;
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

    private static void SetText(Entity entity, string value, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        buffer.AddTag<Dirty>(entity.Id);
    }
}
