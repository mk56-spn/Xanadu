using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Screens.Shared;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Play;

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

        Query.EachEntity(new ShowScore(Query.Store, CommandBuffer));
    }

    /// <summary>The judgement's look <paramref name="age"/> ms after it landed (<see cref="JudgementPop"/>).</summary>
    private static void Pop(Entity text, Color color, float age, CommandBuffer buffer)
    {
        ref TextStyle style = ref text.GetComponent<TextStyle>();
        int size = JudgementPop.FontSize(age);
        Color shown = JudgementPop.Color(color, age);
        if (style.Color != shown || style.FontSize != size)
        {
            style.Color = shown;
            style.FontSize = size;
            buffer.AddTag<Dirty>(text.Id);
        }

        if (text.HasComponent<TextGlow>())
            text.GetComponent<TextGlow>().Color = color with { A = 0.8f * JudgementPop.Fade(age) };
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

        UiText.SetColor(indicator.Text, timing == HitTiming.Early ? Palette.Info : Palette.Warning, buffer);

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

        UiText.SetFill(bar.Marker, color, buffer);
    }

    private readonly record struct ShowScore(EntityStore Store, CommandBuffer Buffer) : IEachEntity<ScoreState, Playhead>
    {
        public void Execute(ref ScoreState score, ref Playhead playhead, int id)
        {
            Entity entity = Store.GetEntityById(id);

            UiText.Set(score.ScoreText, $"SCORE {score.Score:0000000}", Buffer);
            UiText.Set(
                score.AccuracyText,
                $"ACC {score.Accuracy * 100:0.00}%  {ScoreRules.RankFor(score.Accuracy)}",
                Buffer);
            bool showJudgement =
                score.HasJudgement &&
                playhead.TimeMs >= score.JudgementTimeMs &&
                playhead.TimeMs - score.JudgementTimeMs <= JudgementPop.ShowMs;

            UiText.Set(
                score.JudgementText,
                showJudgement
                    ? ScoreRules.LabelFor(score.LastJudgement)
                    : "",
                Buffer);

            float age = (float)(playhead.TimeMs - score.JudgementTimeMs);
            if (showJudgement &&
                !score.JudgementText.IsNull &&
                score.JudgementText.HasComponent<TextStyle>())
            {
                Pop(score.JudgementText, ColorFor(score.LastJudgement), age, Buffer);
            }

            if (entity.HasComponent<ErrorBar>())
                UpdateErrorBar(entity, score, showJudgement, Buffer);

            if (entity.TryGetComponent(out TimingIndicator timing))
                UpdateTimingIndicator(entity, timing, score, showJudgement, Buffer);

            if (entity.HasComponent<JudgementCounter>())
            {
                JudgementCounter counter =
                    entity.GetComponent<JudgementCounter>();
                UiText.Set(counter.FlawlessPlus,
                    score.FlawlessPCount.ToString(), Buffer);
                UiText.Set(counter.Flawless,
                    score.FlawlessCount.ToString(), Buffer);
                UiText.Set(counter.Clean,
                    score.CleanCount.ToString(), Buffer);
                UiText.Set(counter.Fair,
                    score.FairCount.ToString(), Buffer);
                UiText.Set(counter.Deficient,
                    score.DeficientCount.ToString(), Buffer);
                UiText.Set(counter.Terrible,
                    score.TerribleCount.ToString(), Buffer);
                UiText.Set(counter.Miss,
                    score.MissCount.ToString(), Buffer);
            }

            if (showJudgement &&
                !score.JudgementText.IsNull &&
                score.JudgementText.HasComponent<Position>())
            {
                // The judgement belongs to the note that was hit, so it always
                // sits on it; it is not a HUD element that can be moved away.
                ref Position position =
                    ref score.JudgementText.GetComponent<Position>();
                position.Value = JudgementScreenPosition(entity, score) - new Vector2(0, JudgementPop.Rise(age));
                Buffer.AddTag<Moved>(score.JudgementText.Id);
            }
        }
    }
}
