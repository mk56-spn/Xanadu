using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

/// <summary>The transition table: which clips may fade into which, how, and how it is stored.</summary>
[TestFixture]
public sealed class RigTransitionTests
{
    [Test]
    public void AnEmptyTableAllowsEveryHandOverWithTheDefaultFade()
    {
        RigPackage package = Package();

        Assert.That(RigTransitions.IsOpen(package), Is.True);
        Assert.That(RigTransitions.Allows(package, "idle", "jump"), Is.True);
        Assert.That(
            RigTransitions.Blend(package, "idle", "jump"),
            Is.EqualTo((RigTransitions.DefaultFade, RigFadeCurve.Linear)));
    }

    [Test]
    public void OnceThereIsARuleOnlyListedHandOversAreAllowed()
    {
        RigPackage package = Package();
        RigTransition rule = RigTransitions.Allow(package, "idle", "jump");
        rule.Fade = 0.3f;
        rule.Curve = RigFadeCurve.Smooth;

        Assert.That(RigTransitions.Allows(package, "idle", "jump"), Is.True);
        Assert.That(RigTransitions.Allows(package, "jump", "idle"), Is.False);
        Assert.That(RigTransitions.Allows(package, "jump", "jump"), Is.True, "staying on a clip is not a hand-over");
        Assert.That(RigTransitions.Blend(package, "idle", "jump"), Is.EqualTo((0.3f, RigFadeCurve.Smooth)));
        Assert.That(RigTransitions.Allow(package, "idle", "jump"), Is.SameAs(rule));

        Assert.That(RigTransitions.Forbid(package, "idle", "jump"), Is.True);
        Assert.That(RigTransitions.IsOpen(package), Is.True);
    }

    [TestCase(RigFadeCurve.Linear, 0.25f)]
    [TestCase(RigFadeCurve.Smooth, 0.15625f)]
    [TestCase(RigFadeCurve.EaseIn, 0.0625f)]
    [TestCase(RigFadeCurve.EaseOut, 0.4375f)]
    public void EveryCurveRunsFromTheOldClipToTheNew(RigFadeCurve curve, float quarter)
    {
        Assert.That(RigTransitions.Weight(curve, 0f), Is.EqualTo(0f));
        Assert.That(RigTransitions.Weight(curve, 1f), Is.EqualTo(1f));
        Assert.That(RigTransitions.Weight(curve, 0.25f), Is.EqualTo(quarter).Within(0.0001f));
        Assert.That(RigTransitions.Weight(curve, 2f), Is.EqualTo(1f), "clamped past the end");
    }

    [Test]
    public void RulesRoundTripThroughTheBinaryFormat()
    {
        RigPackage package = Package();
        RigTransition rule = RigTransitions.Allow(package, "jump", "idle");
        rule.Fade = 0.45f;
        rule.Curve = RigFadeCurve.EaseOut;

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));

        Assert.That(loaded.Transitions, Has.Count.EqualTo(1));
        Assert.That(loaded.Transitions[0].From, Is.EqualTo("jump"));
        Assert.That(loaded.Transitions[0].To, Is.EqualTo("idle"));
        Assert.That(loaded.Transitions[0].Fade, Is.EqualTo(0.45f));
        Assert.That(loaded.Transitions[0].Curve, Is.EqualTo(RigFadeCurve.EaseOut));
        Assert.That(RigPackageCopy.AreEqual(package, loaded), Is.True);
    }

    [Test]
    public void ValidationRejectsRulesThatCannotBeFollowed()
    {
        RigPackage package = Package();
        package.Transitions.Add(new RigTransition { From = "idle", To = "missing" });
        package.Transitions.Add(new RigTransition { From = "idle", To = "idle" });
        package.Transitions.Add(new RigTransition { From = "jump", To = "idle", Fade = -1f });
        package.Transitions.Add(new RigTransition { From = "jump", To = "idle" });
        package.Transitions.Add(new RigTransition { From = "idle", To = "jump", Curve = (RigFadeCurve)42 });

        string issues = string.Join(" ", RigValidation.Validate(package));

        Assert.That(issues, Does.Contain("missing clip"));
        Assert.That(issues, Does.Contain("goes nowhere"));
        Assert.That(issues, Does.Contain("invalid fade"));
        Assert.That(issues, Does.Contain("Duplicate transition"));
        Assert.That(issues, Does.Contain("unknown curve"));
    }

    [Test]
    public void RenamingOrDeletingAClipCarriesItsRules()
    {
        RigPackage package = Package();
        package.Clips.Add(new RigClip { Id = "land", Duration = 1f });
        RigTransitions.Allow(package, "idle", "jump");
        RigTransitions.Allow(package, "jump", "land");

        Assert.That(RigClipAuthoring.Rename(package, package.Clips[1], "leap"), Is.True);
        Assert.That(RigTransitions.Allows(package, "idle", "leap"), Is.True);
        Assert.That(RigTransitions.Allows(package, "leap", "land"), Is.True);

        Assert.That(RigClipAuthoring.Delete(package, package.Clips[2]), Is.True);
        Assert.That(package.Transitions, Has.Count.EqualTo(1));
        Assert.That(RigValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void UndoSnapshotsCopyAndCompareTheTable()
    {
        RigPackage package = Package();
        RigTransitions.Allow(package, "idle", "jump");
        RigPackage copy = RigPackageCopy.Clone(package);

        Assert.That(RigPackageCopy.AreEqual(package, copy), Is.True);
        copy.Transitions[0].Fade = 0.5f;
        Assert.That(RigPackageCopy.AreEqual(package, copy), Is.False);
        Assert.That(package.Transitions[0].Fade, Is.EqualTo(RigTransitions.DefaultFade));
    }

    [Test]
    public void TheGapIsTheFurthestAnyJointJumpsAtTheHandOver()
    {
        RigPackage package = Package();

        (float distance, string bone) = RigTransitions.Gap(package, package.Clips[0], 2f, package.Clips[1]);

        // idle ends with the root at x 10 and jump starts it at x 10; the arm, at
        // a fixed offset from the root, moves with it, so nothing jumps.
        Assert.That(distance, Is.EqualTo(0f).Within(0.001f));

        (distance, bone) = RigTransitions.Gap(package, package.Clips[1], 2f, package.Clips[0]);
        Assert.That(distance, Is.EqualTo(30f).Within(0.001f));
        Assert.That(bone, Is.EqualTo("root"));
    }

    /// <summary>A root and an arm; idle moves the root from x 0 to 10, jump from 10 to 30.</summary>
    private static RigPackage Package()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Skeleton.Bones.Add(
            new RigBone
            {
                Id = "arm",
                Parent = 0,
                Rest = new Transform2D(0f, new Vector2(0, -20))
            });
        package.Clips.Add(Clip("idle", 0f, 10f));
        package.Clips.Add(Clip("jump", 10f, 30f));
        return package;
    }

    private static RigClip Clip(string id, float start, float end)
    {
        var clip = new RigClip { Id = id, Duration = 2f };
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "root",
            Property = RigChannelProperty.TranslationX
        };
        channel.Keyframes.Add(new RigKeyframe { Time = 0f, Value = start, Interpolation = RigInterpolation.Linear });
        channel.Keyframes.Add(new RigKeyframe { Time = 2f, Value = end, Interpolation = RigInterpolation.Linear });
        clip.Channels.Add(channel);
        return clip;
    }
}
