using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>Clips read out of shader text for the editor's timeline, without compiling anything.</summary>
[TestFixture]
public sealed class SlangClipsTests
{
    private static SlangClip[] Find(string code) => SlangClips.Find(code.Replace("\r", "").Split('\n')).ToArray();

    [Test]
    public void AClipOnALoopedClock()
    {
        SlangClip[] clips = Find("""
            float4 shade(Canvas canvas)
            {
                Clip pop = clock.looped(2).clip(0.2, 0.6).eased(eases.outBack);
                return colours.gold * pop.progress;
            }
            """);

        Assert.That(clips, Has.Length.EqualTo(1));
        SlangClip pop = clips[0];
        Assert.That((pop.Name, pop.Line, pop.Column), Is.EqualTo(("pop", 2, 9)));
        Assert.That((pop.Beats, pop.Loop, pop.Start, pop.Length), Is.EqualTo((false, 2.0, 0.2, 0.6)));
        Assert.That(SlangClips.Easings[pop.Easing], Is.EqualTo("outBack"));
    }

    [Test]
    public void ThenAndDelayedFollowOnFromTheClipBefore()
    {
        SlangClip[] clips = Find("""
            Clock loop = clock.looped(2.4);
            Clip slide = loop.clip(0, 0.5).eased(eases.outCubic);
            Clip spin  = slide.then(0.4).eased(eases.inOutSine);
            Clip leave = spin.then(0.3).delayed(0.6).eased(eases.inQuad);
            """);

        Assert.That(clips.Select(c => c.Name), Is.EqualTo(new[] { "slide", "spin", "leave" }));
        Assert.That(clips[1].Start, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(clips[2].Start, Is.EqualTo(1.5).Within(1e-9));
        Assert.That(clips[2].End, Is.EqualTo(1.8).Within(1e-9));
        Assert.That(clips.All(c => c.Loop == 2.4), Is.True);
    }

    [Test]
    public void BeatsAndBounceAndStaggerByAPixelValue()
    {
        SlangClip[] clips = Find("""
            Clip hit = clock.beats.looped(1).clip(0, 0.75);
            float ring = length(t.id);
            Clip rise = clock.looped(2.4).clip(0, 0.45)
                .staggered(ring, 0.07) // per ring
                .bounced();
            """);

        Assert.That(clips[0].Beats, Is.True);
        Assert.That(clips[1].Line, Is.EqualTo(2));
        Assert.That((clips[1].Start, clips[1].Span, clips[1].StaggerStep), Is.EqualTo((0.0, 0.9, 0.07)));
    }

    [Test]
    public void NumberLocalsAndSumsAreWorkedOut()
    {
        SlangClip[] clips = Find("""
            const float beat = 0.5;
            Clip a = clock.looped(beat * 4).clip(beat / 2, 1 - beat).delayed(-0.1);
            """);

        Assert.That((clips[0].Loop, clips[0].Start, clips[0].Length), Is.EqualTo((2.0, 0.15, 0.5)).Using<(double, double, double)>(
            (x, y) => System.Math.Abs(x.Item1 - y.Item1) < 1e-9 && System.Math.Abs(x.Item2 - y.Item2) < 1e-9 && System.Math.Abs(x.Item3 - y.Item3) < 1e-9));
    }

    [Test]
    public void AClipTheCodeDoesntFixIsFoundButNotPlaced()
    {
        SlangClip[] clips = Find("""
            uniform float delay;
            Clip late = clock.looped(3).clip(delay, 1);
            """);

        Assert.That(clips, Has.Length.EqualTo(1));
        Assert.That(clips[0].Fixed, Is.False);
    }

    [Test]
    public void KeysReadAtAClipLandOnIt()
    {
        SlangClip[] clips = Find("""
            Clip trip = clock.looped(3).clip(0.2, 2.2);
            Keys<float2> path = Keys<float2>(float2(-180, 60))
                .to(0.5, float2(0, -90), eases.outQuad)
                .to(1, float2(180, 60), eases.inQuad);
            Keys<colour> tint = Keys<colour>(colours.gold).to(0.25, colours.hotPink);
            dot.move(path.at(trip));
            c.paint(dot, tint.at(trip.progress));
            """);

        Assert.That(clips[0].Keys, Is.EqualTo(new[] { 0, 0.25, 0.5, 1 }));
    }

    [Test]
    public void AClipCanPlayFromAKeyPress()
    {
        SlangClip[] clips = Find("""
            Clip flash = clock.since(keys.action2).clip(0, 0.25).eased(eases.outQuad);
            Clip song = clock.looped(2).clip(0, 1);
            """);

        Assert.That(SlangClips.PlayKeys[clips[0].Pressed], Is.EqualTo("action2"));
        Assert.That((clips[0].Start, clips[0].Length), Is.EqualTo((0.0, 0.25)));
        Assert.That(clips[1].OnKey, Is.False);
    }

    [Test]
    public void AClipCanPlayFromAnEvent()
    {
        SlangClip[] clips = Find("""
            Clip pulse = clock.since(events.hit).clip(0, 0.3).eased(eases.outBack);
            Clip swerve = clock.since(events.moved).clip(0.1, 0.2);
            """);

        Assert.That(clips[0].OnEvent, Is.True);
        Assert.That(SlangClips.EventName(clips[0].Pressed - SlangClips.FirstEvent), Is.EqualTo("hit"));
        Assert.That(SlangClips.EventParts(clips[1].Pressed - SlangClips.FirstEvent), Is.EqualTo(new[] { 10, 11 }));
        Assert.That((clips[1].Start, clips[1].Length), Is.EqualTo((0.1, 0.2)));
    }

    [Test]
    public void CommentedOutClipsAreNotFound()
    {
        Assert.That(Find("// Clip a = clock.clip(0, 1);\n/* Clip b = clock.clip(0, 1); */"), Is.Empty);
    }

    [TestCase(0, 0.3f, 0.3f)]
    [TestCase(2, 0.5f, 0.75f)]
    [TestCase(11, 0.5f, 0.96875f)]
    public void EasingsMatchTheLibrary(int id, float t, float eased) =>
        Assert.That(SlangClips.Ease(id, t), Is.EqualTo(eased).Within(1e-4));

    [Test]
    public void ANoteClockShowsAsEachJudgement()
    {
        SlangClip[] clips = Find("""
            uniform float hit_note;
            float4 shade(Canvas canvas)
            {
                Note note = notes[int(hit_note + 0.5)];
                Clip life = clock.since(note).clip(0, 0.26);
                Clip first = clock.since(notes[0]).clip(0.1, 0.2);
                return colours.gold * life.progress * first.progress;
            }
            """);

        Assert.That(clips.Select(c => c.Name), Is.EqualTo(new[] { "life", "first" }));
        Assert.That(clips.Select(c => c.Pressed - SlangClips.FirstEvent), Is.All.EqualTo(SlangClips.NoteJudged));
        Assert.That((clips[0].Start, clips[0].Length), Is.EqualTo((0.0, 0.26)));
    }
}
