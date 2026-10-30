using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>Clip events: kept in order, passed by playback exactly once, through loops and fades, and saved.</summary>
[TestFixture]
public sealed class XafEventTests
{
    private static XafPackage Package(out XafClip kick, out XafClip idle)
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        kick = XafClipAuthoring.Add(package, "kick");
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
        Package(out XafClip kick, out _);
        XafEventAuthoring.Add(kick, 0.6f, "land");
        int hit = XafEventAuthoring.Add(kick, 0.3f, "Hit Frame");

        Assert.That(kick.Events.Select(item => item.Name), Is.EqualTo(new[] { "hit_frame", "land" }));
        Assert.That(XafEventAuthoring.Move(kick, hit, 0.9f), Is.EqualTo(1));
        Assert.That(XafEventAuthoring.Add(kick, 5f), Is.EqualTo(-1), "outside the clip");
    }

    [Test]
    public void PlaybackPassesEachEventOnceIncludingOneOnTheFirstInstant()
    {
        XafPackage package = Package(out XafClip kick, out _);
        XafEventAuthoring.Add(kick, 0f, "wind_up");
        XafEventAuthoring.Add(kick, 0.5f, "hit");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("kick");

        Assert.That(Play(runtime, 2f), Is.EqualTo(new[] { "kick:wind_up", "kick:hit" }), "held at the end, nothing repeats");
    }

    [Test]
    public void ALoopPassesItsEventsEveryTimeRound()
    {
        XafPackage package = Package(out _, out XafClip idle);
        XafEventAuthoring.Add(idle, 1.9f, "step");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle");

        Assert.That(Play(runtime, 6f).Count(item => item == "idle:step"), Is.EqualTo(3));
    }

    [Test]
    public void AClipFadingInReportsItsEventsAndSeekingReportsNone()
    {
        XafPackage package = Package(out XafClip kick, out _);
        XafEventAuthoring.Add(kick, 0.05f, "hit");
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle");
        Play(runtime, 0.5f);
        runtime.Play("kick", fadeSeconds: 0.3f);

        Assert.That(Play(runtime, 0.2f), Does.Contain("kick:hit"));
        runtime.Seek(0.9f);
        Assert.That(runtime.Fired, Is.Empty);
    }

    [Test]
    public void EventsAreSavedAndOlderFilesHaveNone()
    {
        XafPackage package = Package(out XafClip kick, out _);
        XafEventAuthoring.Add(kick, 0.25f, "hit");

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));
        Assert.That(loaded.Clips.Find(clip => clip.Id == "kick")!.Events.Single().Name, Is.EqualTo("hit"));
        Assert.That(XafPackageCopy.AreEqual(package, loaded), Is.True);
        Assert.That(XafBinary.Read(XafBinary.Write(package, 8)).Clips.All(clip => clip.Events.Count == 0), Is.True);
    }
}
