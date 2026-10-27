using Position = Xanadu.Ecs.Position;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayErrorBarTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new PlayScoreSystem());
    }

    [Test]
    public void ErrorBarMovesToTheHitOffset()
    {
        Entity marker = Store.CreateEntity(
            new Position(new Vector2(500, 300)),
            new FillColor(new Color(1, 1, 1, 0)));
        Entity playfield = Store.CreateEntity(
            new ScoreState(default, default, default, default, 1),
            new Playhead(0, 120, 0, false),
            new ErrorBar(marker, 500, 300, 180));
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Clean;
        score.LastOffsetMs = 27;

        Frame();

        Assert.That(marker.GetComponent<Position>().Value.X,
            Is.EqualTo(467.6f).Within(0.01f));
        Assert.That(marker.GetComponent<FillColor>().Value,
            Is.EqualTo(PlayScoreSystem.ColorFor(Judgement.Clean)));
    }

    [Test]
    public void ErrorBarHidesOnMiss()
    {
        Entity marker = Store.CreateEntity(
            new Position(new Vector2(500, 300)),
            new FillColor(Colors.White));
        Entity playfield = Store.CreateEntity(
            new ScoreState(default, default, default, default, 1),
            new Playhead(0, 120, 0, false),
            new ErrorBar(marker, 500, 300, 180));
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        score.HasJudgement = true;
        score.LastJudgement = Judgement.Miss;

        Frame();

        Assert.That(marker.GetComponent<FillColor>().Value.A, Is.Zero);
    }
}
