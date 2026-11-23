using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigClipAuthoringTests
{
    private static RigPackage Package() => RigRigAuthoring.CreateEmptyPackage();

    [Test]
    public void IdsStayUnique()
    {
        RigPackage package = Package();

        Assert.That(RigClipAuthoring.UniqueId(package, "walk"), Is.EqualTo("walk"));
        Assert.That(RigClipAuthoring.UniqueId(package, "idle"), Is.EqualTo("idle-2"));
        RigClipAuthoring.Add(package, "idle");
        Assert.That(RigClipAuthoring.UniqueId(package, "idle"), Is.EqualTo("idle-3"));
        Assert.That(RigClipAuthoring.UniqueId(package, "  "), Is.EqualTo("clip"));
    }

    [Test]
    public void AddedClipsAreValidAndAppended()
    {
        RigPackage package = Package();

        RigClip clip = RigClipAuthoring.Add(package);

        Assert.That(package.Clips[^1], Is.SameAs(clip));
        Assert.That(clip.Duration, Is.EqualTo(RigClipAuthoring.DefaultDuration));
        Assert.That(RigValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void DuplicatesAreIndependentDeepCopiesPlacedAfterTheSource()
    {
        RigPackage package = Package();
        RigClip source = package.Clips[0];
        RigAuthoring.SetChannel(source, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 0.5f, 1f);

        RigClip copy = RigClipAuthoring.Duplicate(package, source);

        Assert.That(package.Clips.IndexOf(copy), Is.EqualTo(1));
        Assert.That(copy.Id, Is.EqualTo("idle-2"));
        copy.Channels[0].Keyframes[0].Value = 9f;
        Assert.That(source.Channels[0].Keyframes[0].Value, Is.EqualTo(1f));
    }

    [Test]
    public void TheLastClipCannotBeDeleted()
    {
        RigPackage package = Package();

        while (package.Clips.Count > 1)
            Assert.That(RigClipAuthoring.Delete(package, package.Clips[0]), Is.True);

        Assert.That(RigClipAuthoring.Delete(package, package.Clips[0]), Is.False);
        Assert.That(package.Clips, Has.Count.EqualTo(1));
    }

    [Test]
    public void RenameTrimsAndRefusesEmptyOrTakenNames()
    {
        RigPackage package = Package();
        RigClip idle = package.Clips[0];

        Assert.That(RigClipAuthoring.Rename(package, idle, "  stand  "), Is.True);
        Assert.That(idle.Id, Is.EqualTo("stand"));
        Assert.That(RigClipAuthoring.Rename(package, idle, "   "), Is.False);
        Assert.That(RigClipAuthoring.Rename(package, idle, "death"), Is.False);
        Assert.That(RigClipAuthoring.Rename(package, idle, "stand"), Is.True);
        Assert.That(idle.Id, Is.EqualTo("stand"));
    }

    [Test]
    public void ADurationCannotCutOffKeysOrDropBelowTheMinimum()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];
        clip.Duration = 2f;
        RigAuthoring.SetChannel(clip, RigChannelTarget.Bone, "root", RigChannelProperty.Rotation, 1.5f, 1f);

        Assert.That(RigClipAuthoring.SetDuration(clip, 1f), Is.EqualTo(1.5f));
        Assert.That(RigClipAuthoring.SetDuration(clip, 3f), Is.EqualTo(3f));

        RigClip empty = RigClipAuthoring.Add(package);
        Assert.That(RigClipAuthoring.SetDuration(empty, 0f), Is.EqualTo(RigClipAuthoring.MinDuration));
        Assert.That(RigValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void TurningLoopOnLoopsTheWholeClipAndItFollowsLengthChanges()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];

        RigClipAuthoring.SetLoop(clip, true);
        Assert.That((clip.LoopStart, clip.LoopEnd), Is.EqualTo((0f, 2f)));

        RigClipAuthoring.SetDuration(clip, 3f);
        Assert.That(clip.LoopEnd, Is.EqualTo(3f));
        RigClipAuthoring.SetDuration(clip, 1f);
        Assert.That(clip.LoopEnd, Is.EqualTo(1f));
        Assert.That(RigValidation.Validate(package), Is.Empty);

        RigClipAuthoring.SetLoop(clip, false);
        Assert.That(clip.Loop, Is.False);
    }
}
