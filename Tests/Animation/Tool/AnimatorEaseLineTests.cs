using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Tools.Animator;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorEaseLineTests
{
    private static readonly Rect2 Row = new(0, 100, 1000, 20);
    private const float Bottom = 116f;
    private const float Top = 104f;
    private const float Middle = 110f;

    private static Vector2[] Line(XafEasePreset preset, float from = 100f, float to = 200f) =>
        AnimatorEaseLine.Points(new XafEaseSegment(true, preset), from, to, 0f, 1000f, Row);

    private static float HeightHalfway(Vector2[] line) => line[line.Length / 2].Y;

    [TestCase(XafEasePreset.Linear)]
    [TestCase(XafEasePreset.Ease)]
    [TestCase(XafEasePreset.EaseIn)]
    [TestCase(XafEasePreset.EaseOut)]
    public void EveryCurveClimbsFromOneKeyToTheNext(XafEasePreset preset)
    {
        Vector2[] line = Line(preset);
        Assert.That(line[0], Is.EqualTo(new Vector2(100f, Bottom)));
        Assert.That(line[^1].DistanceTo(new Vector2(200f, Top)), Is.LessThan(0.001f));
    }

    [Test]
    public void TheShapesDifferHalfway()
    {
        Assert.That(HeightHalfway(Line(XafEasePreset.Linear)), Is.EqualTo(Middle).Within(0.01f));
        Assert.That(HeightHalfway(Line(XafEasePreset.Ease)), Is.EqualTo(Middle).Within(0.01f));
        Assert.That(HeightHalfway(Line(XafEasePreset.EaseIn)), Is.GreaterThan(Middle), "still low: slow start");
        Assert.That(HeightHalfway(Line(XafEasePreset.EaseOut)), Is.LessThan(Middle), "already high: fast start");
    }

    [Test]
    public void EaseIsFlatterThanLinearAtTheEnds()
    {
        Vector2[] ease = Line(XafEasePreset.Ease);
        Vector2[] linear = Line(XafEasePreset.Linear);
        Assert.That(ease[1].Y, Is.GreaterThan(linear[1].Y));
    }

    [Test]
    public void StepHoldsThenJumpsAtTheNextKey()
    {
        Assert.That(Line(XafEasePreset.Step), Is.EqualTo(new[]
        {
            new Vector2(100f, Bottom), new Vector2(200f, Bottom), new Vector2(200f, Top),
        }));
    }

    [Test]
    public void NothingChangingOrChannelsDisagreeingIsFlat()
    {
        foreach (var segment in new[] { new XafEaseSegment(false, null), new XafEaseSegment(true, null) })
        {
            Assert.That(AnimatorEaseLine.Points(segment, 100f, 200f, 0f, 1000f, Row),
                Is.EqualTo(new[] { new Vector2(100f, Middle), new Vector2(200f, Middle) }));
        }
    }

    [Test]
    public void ASpanTooNarrowForACurveIsFlat() =>
        Assert.That(Line(XafEasePreset.Ease, 100f, 105f), Has.Length.EqualTo(2));

    [Test]
    public void NothingDrawsOutsideTheVisibleTimeline()
    {
        Vector2[] line = AnimatorEaseLine.Points(new XafEaseSegment(true, XafEasePreset.Linear), 100f, 200f, 150f, 180f, Row);
        foreach (Vector2 point in line)
            Assert.That(point.X, Is.InRange(150f, 180f));
    }

    private static XafClip Clip()
    {
        var clip = new XafClip { Id = "c", Duration = 1f };
        foreach (XafChannelProperty property in new[] { XafChannelProperty.Rotation, XafChannelProperty.ScaleX })
        {
            var channel = new XafChannel { Target = XafChannelTarget.Bone, TargetId = "a", Property = property };
            channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
            channel.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 1f });
            clip.Channels.Add(channel);
        }
        return clip;
    }

    private static XafEaseSegment Read(XafClip clip, XafChannelProperty? property = null, float from = 0f) =>
        XafEasing.ReadSegment(clip, XafChannelTarget.Bone, "a", property, from);

    [Test]
    public void ASegmentReadsTheEasingLeavingTheKey()
    {
        XafClip clip = Clip();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", 0f, XafEasePreset.EaseIn);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, XafEasePreset.EaseIn)));
    }

    [Test]
    public void ChannelsThatDisagreeHaveNoOneEasingOnTheTrackButDoOnTheirOwnRow()
    {
        XafClip clip = Clip();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.Rotation, 0f, XafEasePreset.Step);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, null)));
        Assert.That(Read(clip, XafChannelProperty.Rotation).Preset, Is.EqualTo(XafEasePreset.Step));
        Assert.That(Read(clip, XafChannelProperty.ScaleX).Preset, Is.EqualTo(XafEasePreset.Linear));
    }

    [Test]
    public void AChannelThatDoesNotChangeHasNoSayInTheShape()
    {
        XafClip clip = Clip();
        clip.Channels[1].Keyframes[1].Value = 0f;
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.Rotation, 0f, XafEasePreset.Ease);
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.ScaleX, 0f, XafEasePreset.Step);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, XafEasePreset.Ease)));
    }

    [Test]
    public void NothingChangingReadsAsAHold()
    {
        XafClip clip = Clip();
        clip.Channels[0].Keyframes[1].Value = 0f;
        clip.Channels[1].Keyframes[1].Value = 0f;
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(false, null)));
    }

    [Test]
    public void AChannelStillTravellingFromAnEarlierKeyCounts()
    {
        XafClip clip = Clip();
        clip.Channels[0].Keyframes.Insert(1, new XafKeyframe { Time = 0.5f, Value = 0.5f });
        Assert.That(Read(clip, null, 0.5f).Moves, Is.True);
        Assert.That(Read(clip, XafChannelProperty.ScaleX, 0.5f).Moves, Is.True);
    }
}
