using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Grass in a play scene, loaded from the level document: a meadow along the
/// floor in two colours and a tuft on a block, swaying behind the player, with
/// a polygon rock behind the meadow.
/// </summary>
public sealed class PlayGrassSceneTest : SceneTest
{
    public override string Name => "Play grass";
    public override string Slug => "play-grass";

    protected override void SetUpSteps()
    {
        AddStep("open a play scene with grass", OpenPlay);
        AddUntilStep(
            "wait for the play scene",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Play, UiLayers.PlayHud) &&
                  Context.Entities.Query<GrassPatch>().Count == 3 &&
                  Context.Entities.Query<LevelPolygon>().Count == 1);
        AddAssert("the patches keep their settings", () =>
        {
            foreach (var entity in Context.Entities.Query<GrassPatch>().ToEntityList())
            {
                if (entity.GetComponent<GrassPatch>().Seed == 11)
                    return entity.GetComponent<GrassPatch>().Length == 90f;
            }

            return false;
        });
        AddWaitStep("let it sway", 30);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-grass", "Visual Grass");
        LevelDocument document = TestContent.Level("Grass Play Test", song);
        document.Blocks.Add(new BlockDocument { X = 620, Y = 700, Width = 300, Height = 40 });
        document.Grass.Add(new GrassDocument
        {
            X = 260, Y = 1000, Width = 520, Length = 90, Density = 45, Sway = 18, Seed = 11,
        });
        document.Grass.Add(new GrassDocument
        {
            X = 760, Y = 1000, Width = 480, Length = 60, Density = 70, Sway = 10, Seed = 12,
            Color = new Color("#c8e05a").ToRgba32(), Variation = 0.6f,
        });
        document.Grass.Add(new GrassDocument
        {
            X = 770, Y = 700, Width = 260, Length = 40, Density = 60, Sway = 25, Seed = 13,
        });
        var rock = new Xanadu.Items.Xif();
        Xanadu.Items.XifEditing.AddPolygon(rock, new() { new(-120, 40), new(-60, -50), new(70, -40), new(130, 40) });
        rock.Primitives[0].Fill = new Color("#5b6478");
        document.Polygons.Add(new PolygonDocument { X = 300, Y = 960, Rotation = 0f, Item = rock });
        Context.Scenes.StartPlay(new LevelEntry("memory://play-grass", document, song));
    }
}
