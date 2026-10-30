using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A playfield bigger than the screen: the camera finds the player at the
/// bottom of a 5000-unit field, a hit pops, sparks and kicks, and the combo
/// counter swells as it reaches a milestone.
/// </summary>
public sealed class PlayCameraSceneTest : SceneTest
{
    public override string Name => "Play camera and hit effects";
    public override string Slug => "play-camera";

    protected override void SetUpSteps()
    {
        AddStep("open a 5000-unit play scene", OpenPlay);
        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Play, SceneUi.PlayHudLayer) &&
                  VisualAssert.HasText(Context.Entities, "Visual Camera Test"));
        AddUntilStep("the camera finds the player", () => Playfield().GetComponent<PlayCamera>().Placed);
        AddAssert("the player is in view", () =>
        {
            Entity playfield = Playfield();
            return playfield.GetComponent<PlayCamera>().Visible.HasPoint(playfield.GetComponent<Position>().Value);
        });
        AddAssert("the view has scrolled down the field", () => Playfield().GetComponent<PlayCamera>().Offset.Y > 1000f);
        AddStep("hit a note at a combo milestone", Hit);
        AddWaitStep("let the effects play", 5);
        AddAssert("the note pops", () => Context.Entities.Query<NotePop>().Count > 0);
        AddAssert("the combo shows 50", () => VisualAssert.HasText(Context.Entities, "50"));
        AddAssert("the combo is swelling", () =>
            Playfield().GetComponent<ComboHud>().Punch > 0f);
    }

    private Entity Playfield() => Context.Entities.Query<PlayCamera>().Entities.First();

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-camera", "Visual Camera");
        LevelDocument document = TestContent.Level("Visual Camera Test", song);
        document.PlayfieldWidth = 5000;
        document.PlayfieldHeight = 5000;
        document.Notes.Add(new NoteDocument { TimeMs = 60_000, X = 2300, Y = 4800 });
        document.Notes.Add(new NoteDocument { TimeMs = 61_000, X = 2750, Y = 4650 });
        Context.Scenes.StartPlay(new LevelEntry("visual://camera", document, song));
    }

    private void Hit()
    {
        Entity playfield = Playfield();
        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        foreach (Entity entity in Context.Entities.Query<Note>().ToEntityList().Take(1))
        {
            Note note = entity.GetComponent<Note>();
            HitEffects.Spawn(
                Context.Entities,
                playfield,
                note,
                view.PlayfieldToScreen(note.Position),
                PlayfieldRenderer.ColorFor(note),
                PlayfieldRenderer.NoteSize * view.Scale / 2f);
        }

        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        playfield.GetComponent<ComboHud>().Shown = 49;
        score.Combo = 50;
    }
}
