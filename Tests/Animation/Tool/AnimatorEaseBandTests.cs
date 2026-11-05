using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Tools.Animator;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorEaseBandTests
{
    private static readonly Rect2 Row = new(0, 100, 1000, 20);
    private const float Middle = 110f;
    private const float MaxHalf = 6f;

    private static List<AnimatorEaseBandPiece> Band(XafEasePreset preset, float from = 100f, float to = 200f) =>
        AnimatorEaseBand.Pieces(new XafEaseSegment(true, preset), from, to, 0f, 1000f, Row);

    /// <summary>Half the band's thickness at an x, read off the top edge of the piece covering it.</summary>
    private static float HalfAt(List<AnimatorEaseBandPiece> band, float x)
    {
        foreach (AnimatorEaseBandPiece piece in band)
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
        var band = Band(XafEasePreset.Linear);
        Assert.That(band, Has.Count.EqualTo(1));
        foreach (float x in new[] { 100f, 150f, 200f })
            Assert.That(HalfAt(band, x), Is.EqualTo(MaxHalf).Within(0.01f));
    }

    [Test]
    public void EaseInWidensAndEaseOutNarrows()
    {
        var easeIn = Band(XafEasePreset.EaseIn);
        var easeOut = Band(XafEasePreset.EaseOut);
        Assert.That(HalfAt(easeIn, 101f), Is.LessThan(HalfAt(easeIn, 199f)));
        Assert.That(HalfAt(easeOut, 101f), Is.GreaterThan(HalfAt(easeOut, 199f)));
    }

    [Test]
    public void EaseIsALensThickestHalfway()
    {
        var ease = Band(XafEasePreset.Ease);
        Assert.That(HalfAt(ease, 150f), Is.EqualTo(MaxHalf).Within(0.05f));
        Assert.That(HalfAt(ease, 101f), Is.LessThan(1.5f));
        Assert.That(HalfAt(ease, 199f), Is.LessThan(1.5f));
    }

    [Test]
    public void TheBandNeverThinsPastAHairlineNorSpillsOutOfTheRow()
    {
        foreach (XafEasePreset preset in System.Enum.GetValues<XafEasePreset>())
        foreach (AnimatorEaseBandPiece piece in Band(preset))
        foreach (Vector2 point in piece.Polygon)
            Assert.That(point.Y, Is.InRange(Row.Position.Y, Row.End.Y), preset.ToString());
    }

    [TestCase(XafEasePreset.BackOut)]
    [TestCase(XafEasePreset.ElasticOut)]
    [TestCase(XafEasePreset.BounceOut)]
    public void RunningBackwardsIsItsOwnPiece(XafEasePreset preset) =>
        Assert.That(Band(preset).Any(piece => piece.Reverse), Is.True);

    [TestCase(XafEasePreset.Linear)]
    [TestCase(XafEasePreset.CubicInOut)]
    public void MovingForwardNeverReverses(XafEasePreset preset) =>
        Assert.That(Band(preset).Any(piece => piece.Reverse), Is.False);

    [Test]
    public void StepIsAHairlineThenATallTickAtTheNextKey()
    {
        var band = Band(XafEasePreset.Step);
        Assert.That(band, Has.Count.EqualTo(2));
        Assert.That(band[0].Polygon.Max(p => p.Y) - band[0].Polygon.Min(p => p.Y), Is.EqualTo(2f * AnimatorEaseBand.HairHalf).Within(0.01f));
        Assert.That(band[1].Polygon.Max(p => p.X), Is.EqualTo(200f));
        Assert.That(band[1].Polygon.Max(p => p.Y) - band[1].Polygon.Min(p => p.Y), Is.GreaterThan(2f * MaxHalf));
    }

    [Test]
    public void NothingChangingOrChannelsDisagreeingIsAHairline()
    {
        foreach (var segment in new[] { new XafEaseSegment(false, null), new XafEaseSegment(true, null) })
        {
            var band = AnimatorEaseBand.Pieces(segment, 100f, 200f, 0f, 1000f, Row);
            Assert.That(band, Has.Count.EqualTo(1));
            Assert.That(HalfAt(band, 150f), Is.EqualTo(AnimatorEaseBand.HairHalf).Within(0.01f));
        }
    }

    [Test]
    public void ASpanTooNarrowForAShapeIsAHairline() =>
        Assert.That(HalfAt(Band(XafEasePreset.Ease, 100f, 105f), 102f), Is.EqualTo(AnimatorEaseBand.HairHalf).Within(0.01f));

    [Test]
    public void ScrollingPartOfASpanOffKeepsItsShapeAndStaysInside()
    {
        var whole = Band(XafEasePreset.EaseIn);
        var part = AnimatorEaseBand.Pieces(new XafEaseSegment(true, XafEasePreset.EaseIn), 100f, 200f, 150f, 180f, Row);
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
