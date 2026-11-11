using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Screen-read regions (Docs/render-architecture.md step 11). Two blocks draw
/// the screen through a shader: the top one has only an early baseline region
/// behind it, so it reads the plain field; the bottom one has a region between
/// the coloured band and itself, so it reads the fresh band. Time is held still.
/// </summary>
public sealed class PlayScreenReadSceneTest : SceneTest
{
    public override string Name => "Play screen read";
    public override string Slug => "play-screen-read";

    protected override void SetUpSteps()
    {
        AddStep("start a screen-read level", () =>
        {
            var song = TestContent.Song("visual-play-screen-read", "Play Screen Read");
            LevelDocument document = TestContent.Level("Play Screen Read Test", song);
            document.BackgroundColor = "#202430";

            document.Shaders.Add(new ShaderDocument
            {
                Slot = 1,
                Name = "Read the screen",
                Source = """
                    shader_type canvas_item;
                    render_mode unshaded;
                    uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
                    void fragment() {
                        COLOR = texture(screen_texture, SCREEN_UV);
                    }
                    """,
            });

            // Colour drawn before both readers; each reader sits over one block
            // and shows it only if the back buffer was refreshed after it.
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    document.Blocks.Add(new BlockDocument
                    {
                        X = 90 + column * 260,
                        Y = 90 + row * 260,
                        Width = 200,
                        Height = 200,
                        SelfModulate = new Color(
                            (column + row) % 2 == 0 ? "#ff8a3d" : "#4da3ff").ToRgba32(),
                        Layer = -1,
                        Z = -20 + column + row * 3,
                        OutlineWidth = 0,
                    });
                }
            }

            // A full-screen boundary before anything on the coloured layer, so
            // the top reader's stale read is the plain field.
            document.ScreenReads.Add(new ScreenReadDocument
            {
                X = 0,
                Y = 0,
                Width = 1000,
                Height = 800,
                Layer = -2,
                Z = -100,
            });

            // The reader over the first block, with only the early baseline behind it.
            document.Blocks.Add(Reader(90, 90, z: 0));
            // The same reader, after a region that refreshes the coloured band.
            document.ScreenReads.Add(new ScreenReadDocument
            {
                X = 0,
                Y = 0,
                Width = 1000,
                Height = 800,
                Layer = -1,
                Z = 5,
            });
            // The same reader over a block again, now after a refresh.
            document.Blocks.Add(Reader(350, 90, z: 10));

            document.Texts.Add(new TextDocument
            {
                X = 90,
                Y = 40,
                Text = "STALE",
                Size = 30,
                Layer = 2,
                SelfModulate = new Color("#ffffff").ToRgba32(),
            });
            document.Texts.Add(new TextDocument
            {
                X = 350,
                Y = 40,
                Text = "FRESH",
                Size = 30,
                Layer = 2,
                SelfModulate = new Color("#ffffff").ToRgba32(),
            });

            Context.Scenes.StartPlay(new LevelEntry("memory://play-screen-read", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time after the start", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
        AddWaitStep("let the GO pop fade", 45);
    }

    private static BlockDocument Reader(float x, float y, int z) => new()
    {
        X = x,
        Y = y,
        Width = 200,
        Height = 200,
        SelfModulate = new Color("#ffffff").ToRgba32(),
        Layer = -1,
        Z = z,
        Shader = 1,
        OutlineWidth = 0,
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