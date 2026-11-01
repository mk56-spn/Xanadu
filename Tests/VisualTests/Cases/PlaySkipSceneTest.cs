using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A song with a long entrance offers SKIP once the countdown ends. It lands two
/// seconds before the first note, and a retry offers it again.
/// </summary>
public sealed class PlaySkipSceneTest : SceneTest
{
    private const long FirstNoteMs = 9_000;

    public override string Name => "Play skip entrance";
    public override string Slug => "play-skip";

    protected override void SetUpSteps()
    {
        AddStep("open a play scene with a long entrance", OpenPlay);
        AddUntilStep(
            "wait for the play scene",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Play, UiLayers.PlayHud));
        AddAssert("no skip during the countdown", () => !Session().Started && UiLayout.IsHidden(Button()));
        AddUntilStep("skip appears after the countdown", () => !UiLayout.IsHidden(Button()));

        AddStep("press enter", () => Context.Scenes.TryForward());
        AddUntilStep("lands two seconds before the first note", () => Time() >= FirstNoteMs - 2_000);
        AddUntilStep("skip hides", () => UiLayout.IsHidden(Button()));
        AddAssert("the jump did not overshoot", () => Time() < FirstNoteMs);

        AddStep("retry", () => Context.Scenes.RetryPlay());
        AddUntilStep("skip returns on the retry", () => Session().Started && !UiLayout.IsHidden(Button()));
        AddWaitStep("hold for a look", 6);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-skip", "Visual Skip");
        LevelDocument document = TestContent.Level("Skip Entrance Test", song);
        document.Notes.Add(new NoteDocument { TimeMs = FirstNoteMs, X = 460, Y = 700 });
        Context.Scenes.StartPlay(new LevelEntry("visual://play-skip", document, song));
    }

    private PlaySession Session() => Play().GetComponent<PlaySession>();

    private long Time() => Play().GetComponent<Playhead>().TimeMs;

    private Entity Button() => Session().SkipButton;

    private Entity Play() => Context.Entities.Query<PlaySession>().ToEntityList().First();
}
