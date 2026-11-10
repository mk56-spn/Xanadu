using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Characters;

[TestFixture]
public sealed class CharacterAttackTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new CharacterAttackSystem());

        // Layer time moves with the rig's runtime, as in the game.
        Root.Add(new CharacterRigAnimateSystem());
    }

    private Entity Rigged()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = ContactClip.Idle.Id(), Duration = 2f, Loop = true, LoopEnd = 2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackLow.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackMid.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackHigh.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirLow.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirMid.Id(), Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = ContactClip.AttackAirHigh.Id(), Duration = 0.2f });

        var runtime = new AnimatorRuntime(package);
        runtime.Play(ContactClip.Idle.Id(), 0f);
        return Store.CreateEntity(new CharacterRig(runtime));
    }

    private static AnimatorRuntime RuntimeOf(Entity entity) =>
        entity.GetComponent<CharacterRig>().Runtime;

    [Test]
    public void ANoteAboveThePlayerPlaysTheHighKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackHigh));
        Assert.That(RuntimeOf(player).Additives.Count, Is.EqualTo(1));
        Assert.That(RuntimeOf(player).Additives[0].Clip.Id, Is.EqualTo(ContactClip.AttackHigh.Id()));
    }

    [Test]
    public void ANoteBelowThePlayerPlaysTheLowKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 200, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackLow));
    }

    [Test]
    public void ANoteLevelWithThePlayerPlaysTheMiddleKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 100, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackMid));
    }

    [TestCase(CharacterMotionState.Jumping)]
    [TestCase(CharacterMotionState.Falling)]
    public void AnAirborneRigPlaysTheAirKicks(CharacterMotionState state)
    {
        Entity player = Rigged();
        player.AddComponent(new CharacterMotion { State = state });

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);
        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackAirHigh));

        CharacterAttackSystem.Trigger(player, noteScreenY: 100, playerScreenY: 100);
        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackAirMid));

        CharacterAttackSystem.Trigger(player, noteScreenY: 200, playerScreenY: 100);
        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackAirLow));
    }

    [TestCase(CharacterMotionState.Grounded)]
    [TestCase(CharacterMotionState.Landing)]
    [TestCase(CharacterMotionState.Sliding)]
    public void ARigOnTheGroundPlaysTheGroundKicks(CharacterMotionState state)
    {
        Entity player = Rigged();
        player.AddComponent(new CharacterMotion { State = state });

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackHigh));
    }

    [Test]
    public void TriggeringAgainReplacesTheStillRunningAttackInsteadOfStacking()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);
        CharacterAttackSystem.Trigger(player, noteScreenY: 200, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.EqualTo(ContactClip.AttackLow));
        Assert.That(RuntimeOf(player).Additives.Count, Is.EqualTo(1));
    }

    [Test]
    public void TheLayerIsRemovedOnceItsClipFinishes()
    {
        Entity player = Rigged();
        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);

        Frame(frames: 20); // 20 / 60s > the 0.2s clip duration.

        Assert.That(player.GetComponent<CharacterAttack>().Clip, Is.Null);
        Assert.That(RuntimeOf(player).Additives, Is.Empty);
    }

    [Test]
    public void TriggeringWithoutARigDoesNothing()
    {
        Entity bare = Store.CreateEntity();

        Assert.DoesNotThrow(() => CharacterAttackSystem.Trigger(bare, noteScreenY: 0, playerScreenY: 100));
        Assert.That(bare.HasComponent<CharacterAttack>(), Is.False);
    }
}
