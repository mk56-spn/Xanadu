using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Scoring;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Projects score, accuracy, rank, judgement, and combo into the play HUD.</summary>
public sealed class PlayScoreSystem : QuerySystem<ScoreState, Playhead>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref score, ref playhead, _) =>
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
                score.JudgementText.HasComponent<Position>())
            {
                ref Position position =
                    ref score.JudgementText.GetComponent<Position>();
                position.Value = score.JudgementPosition;
                buffer.AddTag<Moved>(score.JudgementText.Id);
            }
        });
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
