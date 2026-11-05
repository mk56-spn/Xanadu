using Position = Xanadu.Ecs.Position;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

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

    [TestCase(27, "EARLY")]
    [TestCase(-27, "LATE")]
    public void TimingIndicatorNamesTheSideOfTheNote(long offsetMs, string label)
    {
        Entity text = TimingText();
        ref ScoreState score = ref TimingPlayfield(text);
        score.LastJudgement = Judgement.Clean;
        score.LastOffsetMs = offsetMs;
        score.JudgementPosition = new Vector2(400, 200);

        Frame();

        Assert.That(text.Tags.Has<UiHidden>(), Is.False);
        Assert.That(text.GetComponent<Text>().Value, Is.EqualTo(label));
        Assert.That(text.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(400, 200 + PlayScoreSystem.TimingIndicatorGap)));
    }

    [TestCase(Judgement.FlawlessP)]
    [TestCase(Judgement.Miss)]
    [TestCase(Judgement.Mine)]
    [TestCase(Judgement.Contact)]
    public void TimingIndicatorHidesWithoutAnOffsetToCorrect(Judgement judgement)
    {
        Entity text = TimingText();
        ref ScoreState score = ref TimingPlayfield(text);
        score.LastJudgement = judgement;
        score.LastOffsetMs = 5;

        Frame();

        Assert.That(text.Tags.Has<UiHidden>(), Is.True);
    }

    [Test]
    public void TimingIndicatorHidesWhenTheJudgementExpires()
    {
        Entity text = TimingText();
        ref ScoreState score = ref TimingPlayfield(text);
        score.LastJudgement = Judgement.Clean;
        score.LastOffsetMs = 27;
        score.JudgementTimeMs = -701;

        Frame();

        Assert.That(text.Tags.Has<UiHidden>(), Is.True);
    }

    private Entity TimingText() => Store.CreateEntity(
        new Text(""),
        new TextStyle(14, Colors.White),
        new Position(Vector2.Zero));

    private ref ScoreState TimingPlayfield(Entity text)
    {
        Entity playfield = Store.CreateEntity(
            new ScoreState(default, default, default, default, 1),
            new Playhead(0, 120, 0, false),
            new TimingIndicator(text));
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        score.HasJudgement = true;
        return ref score;
    }
}
