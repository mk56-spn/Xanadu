using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafStrikeTests
{
    /// <summary>Idle bends the knee to 0.5 and the hip to 0.2; the kick poses only the knee.</summary>
    private static XafPackage Package(float kickDuration = 0.5f)
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1 });
        package.Skeleton.Bones.Add(new XafBone { Id = "hip", Parent = 0 });
        package.Skeleton.Bones.Add(new XafBone { Id = "knee", Parent = 1, Rest = new Transform2D(0f, new Vector2(0, 40)) });

        var idle = new XafClip { Id = "idle", Duration = 2f, Loop = true, LoopEnd = 2f };
        XafAuthoring.SetChannel(idle, XafChannelTarget.Bone, "knee", XafChannelProperty.Rotation, 0f, 0.5f);
        XafAuthoring.SetChannel(idle, XafChannelTarget.Bone, "hip", XafChannelProperty.Rotation, 0f, 0.2f);
        package.Clips.Add(idle);

        var kick = new XafClip { Id = "kick", Duration = kickDuration };
        XafAuthoring.SetChannel(kick, XafChannelTarget.Bone, "knee", XafChannelProperty.Rotation, 0f, -1f);
        package.Clips.Add(kick);
        return package;
    }

    private static AnimatorRuntime Playing(XafPackage package)
    {
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
        return runtime;
    }

    [Test]
    public void AnOverrideLayerReplacesWhatItKeysAndLeavesTheRest()
    {
        AnimatorRuntime runtime = Playing(Package());

        runtime.AddLayer("kick", 1f, XafLayerBlend.Override);
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
        XafPackage package = Package();
        XafClip kick = package.Clips[1];
        XafEventAuthoring.Add(kick, 0.15f);
        AnimatorRuntime runtime = Playing(package);

        XafStrike.Start(runtime, kick);

        Assert.That(XafStrike.ContactTime(kick), Is.EqualTo(0.15f));
        Assert.That(runtime.Layer("kick")!.Value.Time, Is.EqualTo(0.15f));
        Assert.That(runtime.Layer("kick")!.Value.Blend, Is.EqualTo(XafLayerBlend.Override));
    }

    [Test]
    public void WithoutAContactEventAStrikeStartsAtTheTop()
    {
        Assert.That(XafStrike.ContactTime(Package().Clips[1]), Is.EqualTo(0f));
    }

    [Test]
    public void AStrikeHoldsFullWeightThenFadesBackIntoTheBaseAndEnds()
    {
        XafPackage package = Package();
        XafClip kick = package.Clips[1];
        AnimatorRuntime runtime = Playing(package);
        XafStrike.Start(runtime, kick);

        runtime.Update(0.2f);
        Assert.That(XafStrike.Step(runtime, kick), Is.True);
        Assert.That(runtime.Layer("kick")!.Value.Weight, Is.EqualTo(1f));

        runtime.Update(0.24f);
        Assert.That(XafStrike.Step(runtime, kick), Is.True);
        Assert.That(runtime.Layer("kick")!.Value.Weight, Is.EqualTo(0.5f).Within(0.01f));

        runtime.Update(0.1f);
        Assert.That(XafStrike.Step(runtime, kick), Is.False);
        Assert.That(runtime.Layer("kick"), Is.Null);
    }
}
