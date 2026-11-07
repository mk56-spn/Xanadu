using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The character has gone beats without a hit: she has faded to black with
/// only her hands left, and time is held on a beat that is the left hand's,
/// so it is flashing white while the right is as posed.
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
        AddUntilStep("the rig keeps time", () => Player().HasComponent<CharacterPresence>());
        AddAssert("the rig has both hands", () =>
        {
            Animation.XafPackage package = Player().GetComponent<CharacterRig>().Runtime.Package;
            return Systems.Characters.CharacterPresenceRules.AttachmentOn(package, Systems.Characters.CharacterPresenceRules.LeftHand) >= 0
                   && Systems.Characters.CharacterPresenceRules.AttachmentOn(package, Systems.Characters.CharacterPresenceRules.RightHand) >= 0;
        });
        AddStep("hold time on a left-hand beat, four beats after a hit", () =>
        {
            foreach (Entity entity in Context.Entities.Query<Playhead>().ToEntityList())
            {
                ref Playhead playhead = ref entity.GetComponent<Playhead>();
                long beatMs = (long)Xanadu.Levels.TempoMap.BeatMs(playhead.Tempo.Points[0].Bpm);
                playhead.Playing = false;
                playhead.TimeMs = (long)playhead.Tempo.TimeAt(8);
                playhead.SeekRequestMs = -1;
                Player().GetComponent<CharacterPresence>().LastHitMs = playhead.TimeMs - beatMs * 4;
            }
        });
        AddWaitStep("settle", 6);
    }

    private Entity Player() => Context.Entities.Query<Player, Position>().ToEntityList()[0];
}
