using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Characters;

[TestFixture]
public sealed class CharacterAttackTests : EcsSystemTest
{
    protected override void OnEcsSetUp() =>
        Root.Add(new CharacterAttackSystem());

    private Entity Rigged()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Clips.Add(new XafClip { Id = "idle", Duration = 2f, Loop = true, LoopEnd = 2f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.LowClip, Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.MidClip, Duration = 0.2f });
        package.Clips.Add(new XafClip { Id = CharacterAttackSystem.HighClip, Duration = 0.2f });

        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
        return Store.CreateEntity(new CharacterRig(runtime));
    }

    private static AnimatorRuntime RuntimeOf(Entity entity) =>
        entity.GetComponent<CharacterRig>().Runtime;

    [Test]
    public void ANoteAboveThePlayerPlaysTheHighKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().ClipId, Is.EqualTo(CharacterAttackSystem.HighClip));
        Assert.That(RuntimeOf(player).Additives.Count, Is.EqualTo(1));
        Assert.That(RuntimeOf(player).Additives[0].Clip.Id, Is.EqualTo(CharacterAttackSystem.HighClip));
    }

    [Test]
    public void ANoteBelowThePlayerPlaysTheLowKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 200, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().ClipId, Is.EqualTo(CharacterAttackSystem.LowClip));
    }

    [Test]
    public void ANoteLevelWithThePlayerPlaysTheMiddleKick()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 100, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().ClipId, Is.EqualTo(CharacterAttackSystem.MidClip));
    }

    [Test]
    public void TriggeringAgainReplacesTheStillRunningAttackInsteadOfStacking()
    {
        Entity player = Rigged();

        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);
        CharacterAttackSystem.Trigger(player, noteScreenY: 200, playerScreenY: 100);

        Assert.That(player.GetComponent<CharacterAttack>().ClipId, Is.EqualTo(CharacterAttackSystem.LowClip));
        Assert.That(RuntimeOf(player).Additives.Count, Is.EqualTo(1));
    }

    [Test]
    public void TheLayerIsRemovedOnceItsClipFinishes()
    {
        Entity player = Rigged();
        CharacterAttackSystem.Trigger(player, noteScreenY: 0, playerScreenY: 100);

        Frame(frames: 20); // 20 / 60s > the 0.2s clip duration.

        Assert.That(player.GetComponent<CharacterAttack>().ClipId, Is.Empty);
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
