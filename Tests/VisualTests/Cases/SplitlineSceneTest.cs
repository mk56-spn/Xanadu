using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

/// <summary>Regression: an absorbed Splitline side draws its rail and taps.</summary>
public sealed class SplitlineSceneTest : SceneTest
{
    public override string Name => "Splitline rail";
    public override string Slug => "splitline-rail";

    protected override void SetUpSteps()
    {
        AddStep("open splitline play scene", OpenPlay);
        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                SceneUi.PlayHudLayer) &&
                Context.Entities.Query<Player>().Count == 1);
        AddStep("absorb the left side", AbsorbLeft);
        AddUntilStep(
            "rail notes exist",
            () => Context.Entities.Query<Note>().Count >= 3);
        AddWaitStep("let the rail settle", 4);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-splitline", "Visual Splitline");
        LevelDocument document = TestContent.Level("Splitline Test", song);
        Context.Scenes.StartPlay(
            new LevelEntry("visual://splitline", document, song));
    }

    private void AbsorbLeft()
    {
        Entity playfield = Context.Entities.Query<Player>().ToEntityList()[0];
        playfield.GetComponent<PlayerCharacter>().Kind = CharacterKind.SplitLine;
        Playhead playhead = playfield.GetComponent<Playhead>();
        playfield.GetComponent<SplitlineState>() = new SplitlineState
        {
            LeftActive = true,
            LeftEndMs = playhead.TimeMs + 10_000,
        };

        CreateRail(playhead.TimeMs + 300, PairIndex.One, holdMs: 0);
        CreateRail(playhead.TimeMs + 700, PairIndex.Two, holdMs: 500);
        CreateRail(playhead.TimeMs + 1_100, PairIndex.Three, holdMs: 0);
    }

    private void CreateRail(long timeMs, PairIndex index, long holdMs)
    {
        Context.Entities.CreateEntity(new Note(
            timeMs,
            Vector2.Zero,
            kind: NoteKind.Rail,
            pairIndex: index,
            channelSide: ChannelSide.Left,
            holdMs: holdMs));
    }
}
