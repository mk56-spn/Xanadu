using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Items of different kinds interleaved across layers in play, with time held
/// still (docs/render-architecture.md step 3). Left to right: a note between a
/// blue block under it and a green block over it on the same layer; a text
/// behind that green block; a yellow block made first but drawn over the
/// purple one made after it, by z. A red strip on layer 3 crosses the field in
/// front of everything.
/// </summary>
public sealed class PlayLayersSceneTest : SceneTest
{
    public override string Name => "Play layers";
    public override string Slug => "play-layers";

    protected override void SetUpSteps()
    {
        AddStep("start a layered level", () =>
        {
            var song = TestContent.Song("visual-play-layers", "Play Layers");
            LevelDocument document = TestContent.Level("Play Layers Test", song);

            document.Blocks.Add(Block(160, 300, 160, 160, "#3d7bff", layer: -1, z: -5));
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 2_100, X = 300, Y = 380, Target = KeybindAction.Action2, Layer = -1, Z = 0,
            });
            document.Blocks.Add(Block(330, 340, 120, 160, "#3ccf6a", layer: -1, z: 5));
            document.Texts.Add(new TextDocument
            {
                X = 330, Y = 560, Text = "BEHIND", Size = 72, Layer = -2, Z = 0,
                SelfModulate = new Color("#ffffff").ToRgba32(),
            });
            document.Blocks.Add(Block(380, 520, 90, 120, "#3ccf6a", layer: -1, z: 6));

            document.Blocks.Add(Block(640, 280, 180, 180, "#ffd23d", layer: -2, z: 2));
            document.Blocks.Add(Block(720, 360, 180, 180, "#a05cff", layer: -2, z: 1));

            document.Blocks.Add(Block(0, 470, 1000, 60, "#ff4060", layer: 3, z: 0));
            Context.Scenes.StartPlay(new LevelEntry("memory://play-layers", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time before the note", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
    }

    private static BlockDocument Block(float x, float y, float width, float height, string color, int layer, int z) =>
        new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            SelfModulate = new Color(color).ToRgba32(),
            Layer = layer,
            Z = z,
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
