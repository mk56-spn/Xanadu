using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

[TestFixture]
public sealed class XafClipAuthoringTests
{
    private static XafPackage Package() => XafRigAuthoring.CreateEmptyPackage();

    [Test]
    public void IdsStayUnique()
    {
        XafPackage package = Package();

        Assert.That(XafClipAuthoring.UniqueId(package, "walk"), Is.EqualTo("walk"));
        Assert.That(XafClipAuthoring.UniqueId(package, "idle"), Is.EqualTo("idle-2"));
        XafClipAuthoring.Add(package, "idle");
        Assert.That(XafClipAuthoring.UniqueId(package, "idle"), Is.EqualTo("idle-3"));
        Assert.That(XafClipAuthoring.UniqueId(package, "  "), Is.EqualTo("clip"));
    }

    [Test]
    public void AddedClipsAreValidAndAppended()
    {
        XafPackage package = Package();

        XafClip clip = XafClipAuthoring.Add(package);

        Assert.That(package.Clips[^1], Is.SameAs(clip));
        Assert.That(clip.Duration, Is.EqualTo(XafClipAuthoring.DefaultDuration));
        Assert.That(XafValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void DuplicatesAreIndependentDeepCopiesPlacedAfterTheSource()
    {
        XafPackage package = Package();
        XafClip source = package.Clips[0];
        XafAuthoring.SetChannel(source, XafChannelTarget.Bone, "root",
            XafChannelProperty.Rotation, 0.5f, 1f);

        XafClip copy = XafClipAuthoring.Duplicate(package, source);

        Assert.That(package.Clips.IndexOf(copy), Is.EqualTo(1));
        Assert.That(copy.Id, Is.EqualTo("idle-2"));
        copy.Channels[0].Keyframes[0].Value = 9f;
        Assert.That(source.Channels[0].Keyframes[0].Value, Is.EqualTo(1f));
    }

    [Test]
    public void TheLastClipCannotBeDeleted()
    {
        XafPackage package = Package();

        while (package.Clips.Count > 1)
            Assert.That(XafClipAuthoring.Delete(package, package.Clips[0]), Is.True);

        Assert.That(XafClipAuthoring.Delete(package, package.Clips[0]), Is.False);
        Assert.That(package.Clips, Has.Count.EqualTo(1));
    }

    [Test]
    public void RenameTrimsAndRefusesEmptyOrTakenNames()
    {
        XafPackage package = Package();
        XafClip idle = package.Clips[0];

        Assert.That(XafClipAuthoring.Rename(package, idle, "  stand  "), Is.True);
        Assert.That(idle.Id, Is.EqualTo("stand"));
        Assert.That(XafClipAuthoring.Rename(package, idle, "   "), Is.False);
        Assert.That(XafClipAuthoring.Rename(package, idle, "death"), Is.False);
        Assert.That(XafClipAuthoring.Rename(package, idle, "stand"), Is.True);
        Assert.That(idle.Id, Is.EqualTo("stand"));
    }

    [Test]
    public void ADurationCannotCutOffKeysOrDropBelowTheMinimum()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];
        clip.Duration = 2f;
        XafAuthoring.SetChannel(clip, XafChannelTarget.Bone, "root",
            XafChannelProperty.Rotation, 1.5f, 1f);

        Assert.That(XafClipAuthoring.SetDuration(clip, 1f), Is.EqualTo(1.5f));
        Assert.That(XafClipAuthoring.SetDuration(clip, 3f), Is.EqualTo(3f));

        XafClip empty = XafClipAuthoring.Add(package);
        Assert.That(XafClipAuthoring.SetDuration(empty, 0f), Is.EqualTo(XafClipAuthoring.MinDuration));
        Assert.That(XafValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void TurningLoopOnLoopsTheWholeClipAndItFollowsLengthChanges()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];

        XafClipAuthoring.SetLoop(clip, true);
        Assert.That((clip.LoopStart, clip.LoopEnd), Is.EqualTo((0f, 2f)));

        XafClipAuthoring.SetDuration(clip, 3f);
        Assert.That(clip.LoopEnd, Is.EqualTo(3f));
        XafClipAuthoring.SetDuration(clip, 1f);
        Assert.That(clip.LoopEnd, Is.EqualTo(1f));
        Assert.That(XafValidation.Validate(package), Is.Empty);

        XafClipAuthoring.SetLoop(clip, false);
        Assert.That(clip.Loop, Is.False);
    }
}
