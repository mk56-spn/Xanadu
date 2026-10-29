using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>The transition table: which clips may fade into which, how, and how it is stored.</summary>
[TestFixture]
public sealed class XafTransitionTests
{
    [Test]
    public void AnEmptyTableAllowsEveryHandOverWithTheDefaultFade()
    {
        XafPackage package = Package();

        Assert.That(XafTransitions.IsOpen(package), Is.True);
        Assert.That(XafTransitions.Allows(package, "idle", "jump"), Is.True);
        Assert.That(XafTransitions.Blend(package, "idle", "jump"),
            Is.EqualTo((XafTransitions.DefaultFade, XafFadeCurve.Linear)));
    }

    [Test]
    public void OnceThereIsARuleOnlyListedHandOversAreAllowed()
    {
        XafPackage package = Package();
        XafTransition rule = XafTransitions.Allow(package, "idle", "jump");
        rule.Fade = 0.3f;
        rule.Curve = XafFadeCurve.Smooth;

        Assert.That(XafTransitions.Allows(package, "idle", "jump"), Is.True);
        Assert.That(XafTransitions.Allows(package, "jump", "idle"), Is.False);
        Assert.That(XafTransitions.Allows(package, "jump", "jump"), Is.True, "staying on a clip is not a hand-over");
        Assert.That(XafTransitions.Blend(package, "idle", "jump"), Is.EqualTo((0.3f, XafFadeCurve.Smooth)));
        Assert.That(XafTransitions.Allow(package, "idle", "jump"), Is.SameAs(rule));

        Assert.That(XafTransitions.Forbid(package, "idle", "jump"), Is.True);
        Assert.That(XafTransitions.IsOpen(package), Is.True);
    }

    [TestCase(XafFadeCurve.Linear, 0.25f)]
    [TestCase(XafFadeCurve.Smooth, 0.15625f)]
    [TestCase(XafFadeCurve.EaseIn, 0.0625f)]
    [TestCase(XafFadeCurve.EaseOut, 0.4375f)]
    public void EveryCurveRunsFromTheOldClipToTheNew(XafFadeCurve curve, float quarter)
    {
        Assert.That(XafTransitions.Weight(curve, 0f), Is.EqualTo(0f));
        Assert.That(XafTransitions.Weight(curve, 1f), Is.EqualTo(1f));
        Assert.That(XafTransitions.Weight(curve, 0.25f), Is.EqualTo(quarter).Within(0.0001f));
        Assert.That(XafTransitions.Weight(curve, 2f), Is.EqualTo(1f), "clamped past the end");
    }

    [Test]
    public void RulesRoundTripThroughTheBinaryFormat()
    {
        XafPackage package = Package();
        XafTransition rule = XafTransitions.Allow(package, "jump", "idle");
        rule.Fade = 0.45f;
        rule.Curve = XafFadeCurve.EaseOut;

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(loaded.Transitions, Has.Count.EqualTo(1));
        Assert.That(loaded.Transitions[0].From, Is.EqualTo("jump"));
        Assert.That(loaded.Transitions[0].To, Is.EqualTo("idle"));
        Assert.That(loaded.Transitions[0].Fade, Is.EqualTo(0.45f));
        Assert.That(loaded.Transitions[0].Curve, Is.EqualTo(XafFadeCurve.EaseOut));
        Assert.That(XafPackageCopy.AreEqual(package, loaded), Is.True);
    }

    [Test]
    public void FilesFromBeforeTheTableReadAsOpen()
    {
        XafPackage loaded = XafBinary.Read(XafBinary.Write(Package(), 3));

        Assert.That(loaded.Minor, Is.EqualTo(3));
        Assert.That(XafTransitions.IsOpen(loaded), Is.True);
    }

    [Test]
    public void ValidationRejectsRulesThatCannotBeFollowed()
    {
        XafPackage package = Package();
        package.Transitions.Add(new XafTransition { From = "idle", To = "missing" });
        package.Transitions.Add(new XafTransition { From = "idle", To = "idle" });
        package.Transitions.Add(new XafTransition { From = "jump", To = "idle", Fade = -1f });
        package.Transitions.Add(new XafTransition { From = "jump", To = "idle" });
        package.Transitions.Add(new XafTransition { From = "idle", To = "jump", Curve = (XafFadeCurve)42 });

        string issues = string.Join(" ", XafValidation.Validate(package));

        Assert.That(issues, Does.Contain("missing clip"));
        Assert.That(issues, Does.Contain("goes nowhere"));
        Assert.That(issues, Does.Contain("invalid fade"));
        Assert.That(issues, Does.Contain("Duplicate transition"));
        Assert.That(issues, Does.Contain("unknown curve"));
    }

    [Test]
    public void RenamingOrDeletingAClipCarriesItsRules()
    {
        XafPackage package = Package();
        package.Clips.Add(new XafClip { Id = "land", Duration = 1f });
        XafTransitions.Allow(package, "idle", "jump");
        XafTransitions.Allow(package, "jump", "land");

        Assert.That(XafClipAuthoring.Rename(package, package.Clips[1], "leap"), Is.True);
        Assert.That(XafTransitions.Allows(package, "idle", "leap"), Is.True);
        Assert.That(XafTransitions.Allows(package, "leap", "land"), Is.True);

        Assert.That(XafClipAuthoring.Delete(package, package.Clips[2]), Is.True);
        Assert.That(package.Transitions, Has.Count.EqualTo(1));
        Assert.That(XafValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void UndoSnapshotsCopyAndCompareTheTable()
    {
        XafPackage package = Package();
        XafTransitions.Allow(package, "idle", "jump");
        XafPackage copy = XafPackageCopy.Clone(package);

        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.True);
        copy.Transitions[0].Fade = 0.5f;
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.False);
        Assert.That(package.Transitions[0].Fade, Is.EqualTo(XafTransitions.DefaultFade));
    }

    [Test]
    public void TheGapIsTheFurthestAnyJointJumpsAtTheHandOver()
    {
        XafPackage package = Package();

        (float distance, string bone) = XafTransitions.Gap(package, package.Clips[0], 2f, package.Clips[1]);

        // idle ends with the root at x 10 and jump starts it at x 10; the arm, at
        // a fixed offset from the root, moves with it, so nothing jumps.
        Assert.That(distance, Is.EqualTo(0f).Within(0.001f));

        (distance, bone) = XafTransitions.Gap(package, package.Clips[1], 2f, package.Clips[0]);
        Assert.That(distance, Is.EqualTo(30f).Within(0.001f));
        Assert.That(bone, Is.EqualTo("root"));
    }

    /// <summary>A root and an arm; idle moves the root from x 0 to 10, jump from 10 to 30.</summary>
    private static XafPackage Package()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "arm",
            Parent = 0,
            Rest = new Transform2D(0f, new Vector2(0, -20)),
        });
        package.Clips.Add(Clip("idle", 0f, 10f));
        package.Clips.Add(Clip("jump", 10f, 30f));
        return package;
    }

    private static XafClip Clip(string id, float start, float end)
    {
        var clip = new XafClip { Id = id, Duration = 2f };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "root",
            Property = XafChannelProperty.TranslationX,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = start, Interpolation = XafInterpolation.Linear });
        channel.Keyframes.Add(new XafKeyframe { Time = 2f, Value = end, Interpolation = XafInterpolation.Linear });
        clip.Channels.Add(channel);
        return clip;
    }
}
