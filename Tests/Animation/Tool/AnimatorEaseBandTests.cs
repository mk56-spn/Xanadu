using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Timeline;
using Xanadu.Tools.Animator;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorEaseBandTests
{
    private static readonly Rect2 Row = new(0, 100, 1000, 20);
    private const float Middle = 110f;
    private const float MaxHalf = 6f;

    private static List<EaseBandPiece> Band(EasePreset preset, float from = 100f, float to = 200f) =>
        TimelineEaseBand.Pieces(preset, from, to, 0f, 1000f, Row);

    /// <summary>Half the band's thickness at an x, read off the top edge of the piece covering it.</summary>
    private static float HalfAt(List<EaseBandPiece> band, float x)
    {
        foreach (EaseBandPiece piece in band)
        {
            Vector2[] top = piece.Polygon.Take(piece.Polygon.Length / 2).ToArray();
            for (int i = 0; i + 1 < top.Length; i++)
            {
                if (x >= top[i].X && x <= top[i + 1].X)
                {
                    float y = Mathf.Lerp(top[i].Y, top[i + 1].Y, (x - top[i].X) / (top[i + 1].X - top[i].X));
                    return Middle - y;
                }
            }
        }
        return float.NaN;
    }

    [Test]
    public void LinearIsAnEvenBarAtFullThickness()
    {
        var band = Band(EasePreset.Linear);
        Assert.That(band, Has.Count.EqualTo(1));
        foreach (float x in new[] { 100f, 150f, 200f })
            Assert.That(HalfAt(band, x), Is.EqualTo(MaxHalf).Within(0.01f));
    }

    [Test]
    public void EaseInWidensAndEaseOutNarrows()
    {
        var easeIn = Band(EasePreset.EaseIn);
        var easeOut = Band(EasePreset.EaseOut);
        Assert.That(HalfAt(easeIn, 101f), Is.LessThan(HalfAt(easeIn, 199f)));
        Assert.That(HalfAt(easeOut, 101f), Is.GreaterThan(HalfAt(easeOut, 199f)));
    }

    [Test]
    public void EaseIsALensThickestHalfway()
    {
        var ease = Band(EasePreset.Ease);
        Assert.That(HalfAt(ease, 150f), Is.EqualTo(MaxHalf).Within(0.05f));
        Assert.That(HalfAt(ease, 101f), Is.LessThan(1.5f));
        Assert.That(HalfAt(ease, 199f), Is.LessThan(1.5f));
    }

    [Test]
    public void TheBandNeverThinsPastAHairlineNorSpillsOutOfTheRow()
    {
        foreach (EasePreset preset in System.Enum.GetValues<EasePreset>())
        foreach (EaseBandPiece piece in Band(preset))
        foreach (Vector2 point in piece.Polygon)
            Assert.That(point.Y, Is.InRange(Row.Position.Y, Row.End.Y), preset.ToString());
    }

    [TestCase(EasePreset.BackOut)]
    [TestCase(EasePreset.ElasticOut)]
    [TestCase(EasePreset.BounceOut)]
    public void RunningBackwardsIsItsOwnPiece(EasePreset preset) =>
        Assert.That(Band(preset).Any(piece => piece.Reverse), Is.True);

    [TestCase(EasePreset.Linear)]
    [TestCase(EasePreset.CubicInOut)]
    public void MovingForwardNeverReverses(EasePreset preset) =>
        Assert.That(Band(preset).Any(piece => piece.Reverse), Is.False);

    [Test]
    public void StepIsAHairlineThenATallTickAtTheNextKey()
    {
        var band = Band(EasePreset.Step);
        Assert.That(band, Has.Count.EqualTo(2));
        Assert.That(band[0].Polygon.Max(p => p.Y) - band[0].Polygon.Min(p => p.Y), Is.EqualTo(2f * TimelineEaseBand.HairHalf).Within(0.01f));
        Assert.That(band[1].Polygon.Max(p => p.X), Is.EqualTo(200f));
        Assert.That(band[1].Polygon.Max(p => p.Y) - band[1].Polygon.Min(p => p.Y), Is.GreaterThan(2f * MaxHalf));
    }

    [Test]
    public void NothingChangingOrChannelsDisagreeingIsAHairline()
    {
        foreach (var segment in new[] { new XafEaseSegment(false, null), new XafEaseSegment(true, null) })
        {
            var band = TimelineEaseBand.Pieces(segment.Preset, 100f, 200f, 0f, 1000f, Row);
            Assert.That(band, Has.Count.EqualTo(1));
            Assert.That(HalfAt(band, 150f), Is.EqualTo(TimelineEaseBand.HairHalf).Within(0.01f));
        }
    }

    [Test]
    public void ASpanTooNarrowForAShapeIsAHairline() =>
        Assert.That(HalfAt(Band(EasePreset.Ease, 100f, 105f), 102f), Is.EqualTo(TimelineEaseBand.HairHalf).Within(0.01f));

    [Test]
    public void ScrollingPartOfASpanOffKeepsItsShapeAndStaysInside()
    {
        var whole = Band(EasePreset.EaseIn);
        var part = TimelineEaseBand.Pieces(EasePreset.EaseIn, 100f, 200f, 150f, 180f, Row);
        foreach (Vector2 point in part.SelectMany(piece => piece.Polygon))
            Assert.That(point.X, Is.InRange(150f, 180f));
        Assert.That(HalfAt(part, 165f), Is.EqualTo(HalfAt(whole, 165f)).Within(0.05f));
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
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", 0f, EasePreset.EaseIn);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, EasePreset.EaseIn)));
    }

    [Test]
    public void ChannelsThatDisagreeHaveNoOneEasingOnTheTrackButDoOnTheirOwnRow()
    {
        XafClip clip = Clip();
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.Rotation, 0f, EasePreset.Step);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, null)));
        Assert.That(Read(clip, XafChannelProperty.Rotation).Preset, Is.EqualTo(EasePreset.Step));
        Assert.That(Read(clip, XafChannelProperty.ScaleX).Preset, Is.EqualTo(EasePreset.Linear));
    }

    [Test]
    public void AChannelThatDoesNotChangeHasNoSayInTheShape()
    {
        XafClip clip = Clip();
        clip.Channels[1].Keyframes[1].Value = 0f;
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.Rotation, 0f, EasePreset.Ease);
        XafEasing.Apply(clip, XafChannelTarget.Bone, "a", XafChannelProperty.ScaleX, 0f, EasePreset.Step);
        Assert.That(Read(clip), Is.EqualTo(new XafEaseSegment(true, EasePreset.Ease)));
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
