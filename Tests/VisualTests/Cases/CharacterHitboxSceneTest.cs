using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>The contact rig over its F6 hitbox, standing on the floor and facing one way.</summary>
public sealed class CharacterHitboxSceneTest(bool faceLeft) : SceneTest
{
    public override string Name => faceLeft ? "Character hitbox, facing left" : "Character hitbox, facing right";
    public override string Slug => faceLeft ? "character-hitbox-left" : "character-hitbox-right";

    protected override void SetUpSteps()
    {
        AddStep("start a chart", () =>
        {
            var song = TestContent.Song("visual-character-hitbox", "Character Hitbox");
            LevelDocument document = TestContent.Level("Character Hitbox Test", song);
            document.Notes.Add(new NoteDocument { TimeMs = 20_000, X = 500, Y = 300, Target = KeybindAction.Action3 });
            Context.Scenes.StartPlay(new LevelEntry("memory://character-hitbox", document, song));
        });
        AddUntilStep(
            "wait for play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddStep("dress the player in the contact rig", () => TestContent.DressInContactRig(Player()));
        AddUntilStep("the rig is on screen", () => Player().Tags.Has<CharacterRigVisible>());
        AddStep("face the way under test", () =>
        {
            Entity player = Player();
            if (!player.HasComponent<CharacterFacing>())
                player.AddComponent(new CharacterFacing());
            player.GetComponent<CharacterFacing>().Left = faceLeft;
        });
        AddStep("press F6", () => Context.Keyboard.Press(DebugKeys.Hitboxes));
        AddWaitStep("hold", 2);
        AddStep("release F6", () => Context.Keyboard.Release(DebugKeys.Hitboxes));
        AddWaitStep("settle on the floor", 60);
    }

    private Entity Player() => Context.Entities.Query<Player, Position>().ToEntityList()[0];
}
