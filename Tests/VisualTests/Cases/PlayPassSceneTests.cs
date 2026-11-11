using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Timeline;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A self-contained back layer drawn into its own SubViewport and blurred by
/// its pass, with a sharp text in front of it (Docs/render-architecture.md
/// step 9). Time is held still.
/// </summary>
public sealed class PlayPassBlurSceneTest : SceneTest
{
    public override string Name => "Play pass blur";
    public override string Slug => "play-pass-blur";

    protected override void SetUpSteps()
    {
        AddStep("start a blurred-layer level", () =>
        {
            var song = TestContent.Song("visual-play-pass-blur", "Play Pass Blur");
            LevelDocument document = TestContent.Level("Play Pass Blur Test", song);
            document.BackgroundColor = "#202430";
            document.Shaders.Add(new ShaderDocument
            {
                Slot = 1,
                Name = "Layer blur",
                Source = """
                    shader_type canvas_item;
                    render_mode blend_premul_alpha;
                    instance uniform float radius;
                    void fragment() {
                        vec2 px = TEXTURE_PIXEL_SIZE * radius;
                        vec4 c = texture(TEXTURE, UV + vec2(-px.x, 0.0)) * 0.2;
                        c += texture(TEXTURE, UV + vec2(px.x, 0.0)) * 0.2;
                        c += texture(TEXTURE, UV + vec2(0.0, -px.y)) * 0.2;
                        c += texture(TEXTURE, UV + vec2(0.0, px.y)) * 0.2;
                        c += texture(TEXTURE, UV) * 0.2;
                        COLOR = c;
                    }
                    """,
            });
            document.Layers.Add(new LayerDocument
            {
                Number = -2,
                Name = "Blurred",
                Pass = LayerPassKind.SelfContained,
                PassShader = 1,
                PassKeys =
                {
                    new ShaderKeyDocument
                    {
                        Param = "radius",
                        Time = 0f,
                        Value = new[] { 9f },
                        Ease = EasePreset.Linear,
                    },
                },
            });

            for (int i = 0; i < 4; i++)
            {
                document.Blocks.Add(new BlockDocument
                {
                    X = 150 + i * 150,
                    Y = 250 + (i % 2) * 120,
                    Width = 220,
                    Height = 220,
                    SelfModulate = new Color(i % 2 == 0 ? "#ff8a3d" : "#4da3ff").ToRgba32(),
                    Layer = -2,
                    Z = i,
                });
            }

            document.Texts.Add(new TextDocument
            {
                X = 300,
                Y = 620,
                Text = "SHARP",
                Size = 96,
                Layer = 1,
                SelfModulate = new Color("#ffffff").ToRgba32(),
            });

            Context.Scenes.StartPlay(new LevelEntry("memory://play-pass-blur", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time after the start", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
        AddWaitStep("let the GO pop fade", 45);
    }

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

/// <summary>
/// A background-reader pass on a layer: everything drawn so far is distorted by
/// the pass shader, with a sharp text in front (step 9). Time is held still.
/// </summary>
public sealed class PlayPassDistortSceneTest : SceneTest
{
    public override string Name => "Play pass distort";
    public override string Slug => "play-pass-distort";

    protected override void SetUpSteps()
    {
        AddStep("start a distorted-layer level", () =>
        {
            var song = TestContent.Song("visual-play-pass-distort", "Play Pass Distort");
            LevelDocument document = TestContent.Level("Play Pass Distort Test", song);
            document.BackgroundColor = "#2a3350";
            document.Shaders.Add(new ShaderDocument
            {
                Slot = 1,
                Name = "Layer distort",
                Source = """
                    shader_type canvas_item;
                    uniform sampler2D screen_texture : hint_screen_texture, filter_linear;
                    instance uniform float amount;
                    void fragment() {
                        vec2 uv = SCREEN_UV;
                        uv.x += sin(uv.y * 16.0) * amount * 0.01;
                        COLOR = texture(screen_texture, uv);
                    }
                    """,
            });
            document.Layers.Add(new LayerDocument
            {
                Number = -1,
                Name = "Distorted",
                Pass = LayerPassKind.BackgroundReader,
                PassShader = 1,
                PassKeys =
                {
                    new ShaderKeyDocument
                    {
                        Param = "amount",
                        Time = 0f,
                        Value = new[] { 6f },
                        Ease = EasePreset.Linear,
                    },
                },
            });

            for (int i = 0; i < 3; i++)
            {
                document.Blocks.Add(new BlockDocument
                {
                    X = 200 + i * 200,
                    Y = 260,
                    Width = 160,
                    Height = 160,
                    SelfModulate = new Color(i % 2 == 0 ? "#3ccf6a" : "#ff4060").ToRgba32(),
                    Layer = -1,
                    Z = i,
                });
            }

            document.Texts.Add(new TextDocument
            {
                X = 300,
                Y = 600,
                Text = "UNDISTORTED",
                Size = 72,
                Layer = 1,
                SelfModulate = new Color("#ffffff").ToRgba32(),
            });

            Context.Scenes.StartPlay(new LevelEntry("memory://play-pass-distort", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time after the start", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
        AddWaitStep("let the GO pop fade", 45);
    }

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
