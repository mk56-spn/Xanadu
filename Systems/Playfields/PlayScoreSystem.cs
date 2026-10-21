using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Scoring;

namespace Writer.Systems.Playfields;

/// <summary>Projects score, accuracy, rank, judgement, and combo into the play HUD.</summary>
public sealed class PlayScoreSystem : QuerySystem<ScoreState>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref score, _) =>
        {
            SetText(score.ScoreText, $"SCORE {score.Score:0000000}", buffer);
            SetText(score.ComboText, $"COMBO {score.Combo}", buffer);
            SetText(
                score.AccuracyText,
                $"ACC {score.Accuracy * 100:0.00}%  {ScoreRules.RankFor(score.Accuracy)}",
                buffer);
            SetText(
                score.JudgementText,
                score.HasJudgement
                    ? ScoreRules.LabelFor(score.LastJudgement)
                    : "",
                buffer);
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
