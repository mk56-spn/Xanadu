using Xanadu.Serialisation.Animation;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Model.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Clip events: kept in order, passed by playback exactly once, through loops and fades, and saved.</summary>
[TestFixture]
public sealed class RigEventTests
{
    private static RigPackage Package(out RigClip kick, out RigClip idle)
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        kick = RigClipAuthoring.Add(package, "kick");
        kick.Duration = 1f;
        idle = package.Clips.Find(clip => clip.Id == "idle")!;
        idle.Loop = true;
        idle.LoopStart = 0f;
        idle.LoopEnd = 2f;
        return package;
    }

    private static List<string> Play(AnimatorRuntime runtime, float seconds, float step = 1f / 60f)
    {
        var fired = new List<string>();
        for (float t = 0; t < seconds - 1e-4f; t += step)
        {
            runtime.Update(step);
            fired.AddRange(runtime.Fired.Select(item => $"{item.Clip}:{item.Name}"));
        }

        return fired;
    }

    [Test]
    public void EventsStayInTimeOrderWhereverTheyAreAddedOrMoved()
    {
        Package(out RigClip kick, out _);
        RigEventAuthoring.Add(kick, 0.6f, "land");
        int hit = RigEventAuthoring.Add(kick, 0.3f, "Hit Frame");

        Assert.That(kick.Events.Select(item => item.Name), Is.EqualTo(new[] { "hit_frame", "land" }));
        Assert.That(RigEventAuthoring.Move(kick, hit, 0.9f), Is.EqualTo(1));
        Assert.That(RigEventAuthoring.Add(kick, 5f), Is.EqualTo(-1), "outside the clip");
    }

    [Test]
    public void PlaybackPassesEachEventOnceIncludingOneOnTheFirstInstant()
    {
        RigPackage package = Package(out RigClip kick, out _);
        RigEventAuthoring.Add(kick, 0f, "wind_up");
        RigEventAuthoring.Add(kick, 0.5f, "hit");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("kick");

        Assert.That(
            Play(runtime, 2f),
            Is.EqualTo(new[] { "kick:wind_up", "kick:hit" }),
            "held at the end, nothing repeats");
    }

    [Test]
    public void ALoopPassesItsEventsEveryTimeRound()
    {
        RigPackage package = Package(out _, out RigClip idle);
        RigEventAuthoring.Add(idle, 1.9f, "step");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle");

        Assert.That(Play(runtime, 6f).Count(item => item == "idle:step"), Is.EqualTo(3));
    }

    [Test]
    public void AClipFadingInReportsItsEventsAndSeekingReportsNone()
    {
        RigPackage package = Package(out RigClip kick, out _);
        RigEventAuthoring.Add(kick, 0.05f, "hit");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle");
        Play(runtime, 0.5f);
        runtime.Play("kick", 0.3f);

        Assert.That(Play(runtime, 0.2f), Does.Contain("kick:hit"));
        runtime.Seek(0.9f);
        Assert.That(runtime.Fired, Is.Empty);
    }

    [Test]
    public void EventsAreSaved()
    {
        RigPackage package = Package(out RigClip kick, out _);
        RigEventAuthoring.Add(kick, 0.25f, "hit");

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));
        Assert.That(loaded.Clips.Find(clip => clip.Id == "kick")!.Events.Single().Name, Is.EqualTo("hit"));
        Assert.That(RigPackageCopy.AreEqual(package, loaded), Is.True);
    }
}
