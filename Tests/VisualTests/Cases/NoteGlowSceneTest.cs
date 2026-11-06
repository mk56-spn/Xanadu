using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Notes and their approach rings in play, with time held still: three notes
/// at different distances from their beat, so each ring is a different size,
/// with no glow under either (notes stopped glowing).
/// </summary>
public sealed class NoteGlowSceneTest : SceneTest
{
    public override string Name => "Note glow";
    public override string Slug => "note-glow";

    protected override void SetUpSteps()
    {
        AddStep("start a chart", () =>
        {
            var song = TestContent.Song("visual-note-glow", "Note Glow");
            LevelDocument document = TestContent.Level("Note Glow Test", song);
            document.Notes.Add(new NoteDocument { TimeMs = 2_100, X = 250, Y = 350, Target = KeybindAction.L3 });
            document.Notes.Add(new NoteDocument { TimeMs = 2_400, X = 520, Y = 300, Target = KeybindAction.M });
            document.Notes.Add(new NoteDocument { TimeMs = 2_700, X = 760, Y = 420, Target = KeybindAction.R2 });
            Context.Scenes.StartPlay(new LevelEntry("memory://note-glow", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("hold time before the notes", () => SetPlayheadTime(2_000));
        AddWaitStep("settle", 6);
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
