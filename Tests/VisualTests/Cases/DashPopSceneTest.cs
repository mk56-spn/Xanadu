using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// A dash note is hit: it pops out with sparks, its connector fades and its
/// exit flashes and fades a little after. A second dash note waits beside it,
/// its connector and exit at half strength.
/// </summary>
public sealed class DashPopSceneTest : SceneTest
{
    public override string Name => "Dash pop";
    public override string Slug => "dash-pop";

    /// <summary>How far into the fade the shot is taken, in seconds.</summary>
    private const float ShotAge = 0.12f;

    protected override void SetUpSteps()
    {
        AddStep("start a two-dash chart", () =>
        {
            var song = TestContent.Song("visual-dash-pop", "Dash Pop");
            LevelDocument document = TestContent.Level("Dash Pop Test", song);
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000, X = 500, Y = 1000, Target = KeybindAction.Action3,
                Kind = NoteKind.Dash,
            });
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_300, X = 500, Y = 1000, Target = KeybindAction.Action3,
                Kind = NoteKind.Dash,
                DashStrength = DashStrength.Double,
            });
            Context.Scenes.StartPlay(new LevelEntry("memory://dash-pop", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddUntilStep("wait for the first dash", () => PlayheadTime() >= 900);
        AddStep("put the first dash on the player and the second beside it", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
            PlayfieldView view = Context.Entities.Query<PlayfieldView>().ToEntityList()[0]
                .GetComponent<PlayfieldView>();
            Vector2 at = view.ScreenToPlayfield(player.GetComponent<Position>().Value);
            foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
            {
                ref Note value = ref note.GetComponent<Note>();
                value.Position = value.TimeMs == 1_000 ? at : at + new Vector2(-150, -450);
            }
        });
        AddStep("freeze time", () => SetPlayheadTime(1_000));
        AddStep("hit the dash up", () =>
            Context.Keyboard.Press(Context.Systems.Keybinds.Get(KeybindAction.Up)));
        AddUntilStep("the dash pops", () => Context.Entities.Query<NotePop>().Count > 0);
        AddUntilStep(
            "the pop has aged",
            () => Context.Entities.Query<NotePop>().Count > 0 &&
                  Context.Entities.Query<NotePop>().ToEntityList()[0]
                      .GetComponent<NotePop>().Age >= ShotAge);
        AddAssert("the second dash is still waiting", () => Context.Entities.Query<Note>().Count == 1);
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

    private long PlayheadTime() =>
        Context.Entities.Query<Playhead>().Count == 0
            ? 0
            : Context.Entities.Query<Playhead>().ToEntityList()[0].GetComponent<Playhead>().TimeMs;
}
