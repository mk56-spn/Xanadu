using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigStrikeTests
{
    /// <summary>Idle bends the knee to 0.5 and the hip to 0.2; the kick poses only the knee.</summary>
    private static RigPackage Package(float kickDuration = 0.5f)
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "root", Parent = -1 });
        package.Skeleton.Bones.Add(new RigBone { Id = "hip", Parent = 0 });
        package.Skeleton.Bones.Add(
            new RigBone { Id = "knee", Parent = 1, Rest = new Transform2D(0f, new Vector2(0, 40)) });

        var idle = new RigClip { Id = "idle", Duration = 2f, Loop = true, LoopEnd = 2f };
        RigAuthoring.SetChannel(idle, RigChannelTarget.Bone, "knee", RigChannelProperty.Rotation, 0f, 0.5f);
        RigAuthoring.SetChannel(idle, RigChannelTarget.Bone, "hip", RigChannelProperty.Rotation, 0f, 0.2f);
        package.Clips.Add(idle);

        var kick = new RigClip { Id = "kick", Duration = kickDuration };
        RigAuthoring.SetChannel(kick, RigChannelTarget.Bone, "knee", RigChannelProperty.Rotation, 0f, -1f);
        package.Clips.Add(kick);
        return package;
    }

    private static AnimatorRuntime Playing(RigPackage package)
    {
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
        return runtime;
    }

    [Test]
    public void AnOverrideLayerReplacesWhatItKeysAndLeavesTheRest()
    {
        AnimatorRuntime runtime = Playing(Package());

        runtime.AddLayer("kick", 1f, RigLayerBlend.Override);
        runtime.Update(0.01f);

        Assert.That(runtime.Pose.Bones[2].Rotation, Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(runtime.Pose.Bones[1].Rotation, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(runtime.Pose.Bones[2].Translation, Is.EqualTo(new Vector2(0, 40)));
    }

    [Test]
    public void AnAdditiveLayerStillAddsOnTop()
    {
        AnimatorRuntime runtime = Playing(Package());

        runtime.AddLayer("kick", 1f);
        runtime.Update(0.01f);

        Assert.That(runtime.Pose.Bones[2].Rotation, Is.EqualTo(-0.5f).Within(0.0001f));
    }

    [Test]
    public void AStrikeStartsAtItsContactEvent()
    {
        RigPackage package = Package();
        RigClip kick = package.Clips[1];
        RigEventAuthoring.Add(kick, 0.15f);
        AnimatorRuntime runtime = Playing(package);

        RigStrike.Start(runtime, kick);

        Assert.That(RigStrike.ContactTime(kick), Is.EqualTo(0.15f));
        Assert.That(runtime.Layer("kick")!.Value.Time, Is.EqualTo(0.15f));
        Assert.That(runtime.Layer("kick")!.Value.Blend, Is.EqualTo(RigLayerBlend.Override));
    }

    [Test]
    public void WithoutAContactEventAStrikeStartsAtTheTop() => Assert.That(
        RigStrike.ContactTime(Package().Clips[1]),
        Is.EqualTo(0f));

    [Test]
    public void AStrikeHoldsFullWeightThenFadesBackIntoTheBaseAndEnds()
    {
        RigPackage package = Package();
        RigClip kick = package.Clips[1];
        AnimatorRuntime runtime = Playing(package);
        RigStrike.Start(runtime, kick);

        runtime.Update(0.2f);
        Assert.That(RigStrike.Step(runtime, kick), Is.True);
        Assert.That(runtime.Layer("kick")!.Value.Weight, Is.EqualTo(1f));

        runtime.Update(0.24f);
        Assert.That(RigStrike.Step(runtime, kick), Is.True);
        Assert.That(runtime.Layer("kick")!.Value.Weight, Is.EqualTo(0.5f).Within(0.01f));

        runtime.Update(0.1f);
        Assert.That(RigStrike.Step(runtime, kick), Is.False);
        Assert.That(runtime.Layer("kick"), Is.Null);
    }
}
