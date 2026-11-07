using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The character is seconds from the nearest note: she has flashed out and a
/// rhombus stands in her place, held on a beat so its pulse is at its peak.
/// </summary>
public sealed class CharacterWaitSceneTest : SceneTest
{
    public override string Name => "Character waiting";
    public override string Slug => "character-wait";

    protected override void SetUpSteps()
    {
        AddStep("start a chart with a long gap", () =>
        {
            var song = TestContent.Song("visual-character-wait", "Character Wait");
            LevelDocument document = TestContent.Level("Character Wait Test", song);
            document.Notes.Add(new NoteDocument { TimeMs = 20_000, X = 500, Y = 300, Target = KeybindAction.L3 });
            Context.Scenes.StartPlay(new LevelEntry("memory://character-wait", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("dress the player in the contact rig",
            () => TestContent.DressInContactRig(Player()));
        AddUntilStep("the rig is on screen", () => Player().Tags.Has<CharacterRigVisible>());
        AddAssert("the chart has a rest", () => Player().HasComponent<CharacterPresence>());
        AddStep("hold time on a beat far from the note", () =>
        {
            foreach (Entity entity in Context.Entities.Query<Playhead>().ToEntityList())
            {
                ref Playhead playhead = ref entity.GetComponent<Playhead>();
                playhead.Playing = false;
                playhead.TimeMs = (long)playhead.Tempo.TimeAt(8);
                playhead.SeekRequestMs = -1;
            }
        });
        AddWaitStep("settle", 6);
        AddAssert("she is away", () => !Systems.Characters.CharacterPresenceRules.For(Player()).Shown);
    }

    private Entity Player() => Context.Entities.Query<Player, Position>().ToEntityList()[0];
}
