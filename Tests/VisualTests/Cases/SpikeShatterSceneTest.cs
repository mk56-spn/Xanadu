using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A spike is dropped on the player: the character bursts into a spray of
/// note glyphs and is gone, caught a moment into the burst.
/// </summary>
public sealed class SpikeShatterSceneTest : SceneTest
{
    public override string Name => "Spike shatter";
    public override string Slug => "spike-shatter";

    protected override void SetUpSteps()
    {
        AddStep("start a chart with a spike", () =>
        {
            var song = TestContent.Song("visual-spike-shatter", "Spike Shatter");
            LevelDocument document = TestContent.Level("Spike Shatter Test", song);
            document.Spikes.Add(new SpikeDocument
            {
                X = 0, Y = 0, Width = 100, Height = 100,
            });
            Context.Scenes.StartPlay(
                new LevelEntry("memory://spike-shatter", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddStep("put the spike on the player", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
            PlayfieldView view = Context.Entities.Query<PlayfieldView>().ToEntityList()[0]
                .GetComponent<PlayfieldView>();
            Vector2 at = view.ScreenToPlayfield(player.GetComponent<Position>().Value);
            foreach (Entity spike in Context.Entities.Query<Spike>().ToEntityList())
                spike.GetComponent<Spike>().Position = at - new Vector2(50, 50);
        });
        AddUntilStep(
            "the player shatters",
            () => Context.Entities.Query<Player>().ToEntityList()[0].Tags.Has<PlayerShattered>());
        AddWaitStep("let the burst spread", 9);
        AddAssert(
            "the character is gone",
            () => !Context.Entities.Query<Player>().ToEntityList()[0].Tags.Has<CharacterRigVisible>());
    }
}
