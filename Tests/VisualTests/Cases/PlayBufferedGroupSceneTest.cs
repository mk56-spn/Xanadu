using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Buffered groups (docs/render-architecture.md step 10): two groups of three
/// overlapping blocks, faded the same amount. The left one is buffered, so the
/// overlaps keep the members' own colour and the group fades as one image; the
/// right one fades each member, so its overlaps darken.
/// </summary>
public sealed class PlayBufferedGroupSceneTest : SceneTest
{
    public override string Name => "Play buffered group";
    public override string Slug => "play-buffered-group";

    protected override void SetUpSteps()
    {
        AddStep("start a buffered and an unbuffered group", () =>
        {
            var song = TestContent.Song("visual-play-buffered-group", "Play Buffered Group");
            LevelDocument document = TestContent.Level("Play Buffered Group Test", song);
            document.BackgroundColor = "#202430";

            // Group 1 fades each member; group 2 fades one buffered image.
            document.Groups.Add(new GroupDocument
            {
                Id = 0,
                Name = "Per item",
                Effects =
                {
                    new GroupEffectDocument
                    {
                        Kind = GroupEffectKind.Color,
                        Start = 0f,
                        End = 0.1f,
                        Tint = "#ffffff80",
                    },
                },
            });
            document.Groups.Add(new GroupDocument
            {
                Id = 1,
                Name = "Buffered",
                Buffered = true,
                BufferAlpha = 0.5f,
            });

            for (int i = 0; i < 3; i++)
            {
                document.Blocks.Add(Block(120 + i * 70, 300 + i * 70, group: 0));
                document.Blocks.Add(Block(620 + i * 70, 300 + i * 70, group: 1));
            }

            Context.Scenes.StartPlay(new LevelEntry("memory://play-buffered-group", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time after the start", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
        AddWaitStep("let the GO pop fade", 45);
    }

    private static BlockDocument Block(float x, float y, int group) => new()
    {
        X = x,
        Y = y,
        Width = 180,
        Height = 180,
        SelfModulate = new Color("#ff8a3d").ToRgba32(),
        Layer = -1,
        Z = (int)y,
        Groups = { group },
    };

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
