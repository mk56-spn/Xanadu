using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>The pause overlay, built by the scene system at runtime.</summary>
public sealed class PauseSceneTest : SceneTest
{
    public override string Name => "Pause overlay";
    public override string Slug => "pause";

    protected override void SetUpSteps()
    {
        AddStep("open synthetic play scene", OpenPlay);
        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                SceneUi.PlayHudLayer) &&
                VisualAssert.HasText(Context.Entities, "Visual Pause Test"));
        AddStep("pause", () => Context.Scenes.PausePlay());
        AddUntilStep(
            "wait for pause overlay",
            () => PauseVisible() &&
                  VisualAssert.HasText(Context.Entities, "PAUSED"));
        AddAssert(
            "pause actions exist",
            () => VisualAssert.HasText(Context.Entities, "RESUME") &&
                  VisualAssert.HasText(Context.Entities, "RETRY") &&
                  VisualAssert.HasText(Context.Entities, "LEAVE LEVEL"));
        AddAssert(
            "pause message is shown",
            () => VisualAssert.HasText(
                Context.Entities,
                "Resume, retry, or leave the level."));
        AddAssert(
            "pause overlay is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.PauseLayer) == 0);
    }

    private bool PauseVisible()
    {
        var pauses = Context.Entities.Query<ScenePause>().ToEntityList();
        return pauses.Count == 1 &&
               pauses[0].GetComponent<ScenePause>().Visible;
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-pause", "Visual Pause");
        LevelDocument document = TestContent.Level("Visual Pause Test", song);
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 260, Y = 620 });

        Context.Scenes.StartPlay(new LevelEntry("visual://pause", document, song));
    }
}
