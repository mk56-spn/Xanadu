using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Static blend modes in play (docs/render-architecture.md step 7): three pairs
/// of overlapping blocks over one background, left to right on mix, add and
/// multiply. Mix darkens where they overlap, add brightens, multiply darkens
/// the background under it. Time is held still.
/// </summary>
public sealed class PlayBlendsSceneTest : SceneTest
{
    public override string Name => "Play blends";
    public override string Slug => "play-blends";

    protected override void SetUpSteps()
    {
        AddStep("start a blended level", () =>
        {
            var song = TestContent.Song("visual-play-blends", "Play Blends");
            LevelDocument document = TestContent.Level("Play Blends Test", song);
            document.BackgroundColor = "#3a4258";

            Blend(document, 150, 300, "#66aaffc0", BlendMode.Mix);
            Blend(document, 240, 390, "#66aaffc0", BlendMode.Mix);

            Blend(document, 470, 300, "#55ff99c0", BlendMode.Add);
            Blend(document, 560, 390, "#55ff99c0", BlendMode.Add);

            Blend(document, 790, 300, "#ff77ccc0", BlendMode.Mul);
            Blend(document, 880, 390, "#ff77ccc0", BlendMode.Mul);

            Context.Scenes.StartPlay(new LevelEntry("memory://play-blends", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time after the start", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
        AddWaitStep("let the GO pop fade", 45);
    }

    private static void Blend(LevelDocument document, float x, float y, string color, BlendMode blend) =>
        document.Blocks.Add(new BlockDocument
        {
            X = x,
            Y = y,
            Width = 180,
            Height = 180,
            SelfModulate = new Color(color).ToRgba32(),
            Layer = -1,
            Z = document.Blocks.Count,
            Blend = blend,
        });

    private void SetPlayheadTime(long timeMs)
    {
        foreach (Entity entity in Context.Entities.Query<Playhead>().ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.TimeMs = timeMs;
            playhead.SeekRequestMs = -1;
        }
    }
}
