using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>A spike is one big triangle filling its tile, pointing each of the four ways.</summary>
public sealed class SpikeShapeSceneTest : SceneTest
{
    public override string Name => "Spike shape";
    public override string Slug => "spike-shape";

    protected override void SetUpSteps()
    {
        AddStep("start a chart with spikes", () =>
        {
            var song = TestContent.Song("visual-spikes", "Spikes");
            LevelDocument document = TestContent.Level("Spike Shape Test", song);
            for (int column = 0; column < 4; column++)
            {
                document.Spikes.Add(new SpikeDocument
                {
                    X = 100 + column * 100,
                    Y = 200,
                    Width = 100,
                    Height = 100,
                    RotationDegrees = (column) * 90f,
                });
            }

            Context.Scenes.StartPlay(
                new LevelEntry("memory://spike-shape", document, song));
        });
        AddUntilStep(
            "spikes exist",
            () => Context.Entities.Query<Spike>().Count == 4);
    }
}
