using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Characters;
using Xanadu.Scenes;
using Xanadu.Settings;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The character hits a dash note and dashes off: a see-through copy of it
/// stays where it stood when it hit, fading out.
/// </summary>
public sealed class HitGhostSceneTest : SceneTest
{
    public override string Name => "Hit ghost";
    public override string Slug => "hit-ghost";

    /// <summary>How far into the fade the shot is taken, in seconds.</summary>
    private const float ShotAge = 0.12f;

    protected override void SetUpSteps()
    {
        AddStep("start a dash chart", () =>
        {
            var song = TestContent.Song("visual-hit-ghost", "Hit Ghost");
            LevelDocument document = TestContent.Level("Hit Ghost Test", song);
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000, X = 500, Y = 1000, Target = KeybindAction.L3,
                Kind = NoteKind.Dash, DashDirection = DashDirection.UpRight,
            });
            Context.Scenes.StartPlay(new LevelEntry("memory://hit-ghost", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("dress the player in the contact rig", DressInRig);
        AddUntilStep(
            "the rig is on screen",
            () => Context.Entities.Query<Player, Position>().ToEntityList()[0].Tags.Has<CharacterRigVisible>());
        AddUntilStep("wait for the dash", () => PlayheadTime() >= 900);
        AddStep("put the dash on the player", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
            PlayfieldView view = Context.Entities.Query<PlayfieldView>().ToEntityList()[0]
                .GetComponent<PlayfieldView>();
            Vector2 at = view.ScreenToPlayfield(player.GetComponent<Position>().Value);
            foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
                note.GetComponent<Note>().Position = at;
        });
        AddStep("freeze time", () => SetPlayheadTime(1_000));
        AddStep("hit the dash", () =>
            Context.Keyboard.Press(Context.Systems.Keybinds.Get(KeybindAction.L3)));
        AddUntilStep("a copy is left behind", () => Context.Entities.Query<HitGhost>().Count == 1);
        AddUntilStep(
            "the copy is fading",
            () => Context.Entities.Query<HitGhost>().Count == 1 &&
                  Context.Entities.Query<HitGhost>().ToEntityList()[0].GetComponent<HitGhost>().Age >= ShotAge);
        AddAssert(
            "the copy is the character's art",
            () => Context.Entities.Query<HitGhost>().ToEntityList()[0].Tags.Has<HitGhostRigged>());
    }

    /// <summary>
    /// The harness's <c>res://</c> is its own folder, so the game's rig path
    /// misses; load the same package from the repo instead. The source keeps the
    /// game's path, so the load system leaves the rig alone.
    /// </summary>
    private void DressInRig()
    {
        Entity player = Context.Entities.Query<Player, Position>().ToEntityList()[0];
        if (player.HasComponent<CharacterRig>())
            return;

        string repo = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
        string path = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(repo, "../../Tools/Animator/Animations/Characters/contact.xaf"));
        XafPackage? package = new GodotCharacterRigFiles().Load(path);
        if (package is null)
            return;

        var runtime = new AnimatorRuntime(package);
        runtime.Play(ContactClip.Idle.Id(), 0f);
        player.RemoveTag<CharacterRigMissing>();
        player.AddComponent(new CharacterRig(runtime));
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
