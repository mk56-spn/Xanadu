using System.Linq;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Tests.Groups;

/// <summary>Group effects that repeat in beats, on the song's tempo map.</summary>
[TestFixture]
public sealed class GroupCycleTests
{
    /// <summary>120 BPM from 0: a beat is half a second.</summary>
    private static readonly TempoMap Steady = TempoMap.Single(120, 0);

    private static GroupDefinition[] Slots(GroupEffect effect)
    {
        var groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        groups[0].Effects.Add(effect);
        return groups;
    }

    /// <summary>A quarter-second move of 100 px, four times, a beat apart.</summary>
    private static GroupEffect Pulse(GroupCycle mode) =>
        GroupEffect.Move(0f, 0.25f, new Vector2(100, 0)) with { Cycles = 4, EveryBeats = 1f, Cycle = mode };

    private static float X(GroupEffect effect, float seconds, TempoMap tempo) =>
        GroupModulation.Offset(Slots(effect), ItemGroups.Bit(0), seconds, tempo).X;

    [Test]
    public void RestartRunsEachCycleAndSnapsBack()
    {
        GroupEffect effect = Pulse(GroupCycle.Restart);

        Assert.That(X(effect, 0.125f, Steady), Is.EqualTo(50f).Within(0.01f));
        Assert.That(X(effect, 0.4f, Steady), Is.EqualTo(100f).Within(0.01f), "held until the next cycle");
        Assert.That(X(effect, 0.625f, Steady), Is.EqualTo(50f).Within(0.01f), "the second cycle starts again from nothing");
        Assert.That(X(effect, 9f, Steady), Is.EqualTo(100f).Within(0.01f), "the last cycle's result is held");
    }

    [Test]
    public void ReturnGoesAndComesBackInsideEachCycle()
    {
        GroupEffect effect = Pulse(GroupCycle.Return);

        Assert.That(X(effect, 0.125f, Steady), Is.EqualTo(100f).Within(0.01f), "the peak is half way through");
        Assert.That(X(effect, 0.25f, Steady), Is.EqualTo(0f).Within(0.01f));
        Assert.That(X(effect, 1.625f, Steady), Is.EqualTo(100f).Within(0.01f), "the fourth cycle peaks too");
        Assert.That(X(effect, 9f, Steady), Is.EqualTo(0f).Within(0.01f), "and it ends back home");
    }

    [Test]
    public void StackAddsEachCycleToTheLast()
    {
        GroupEffect effect = Pulse(GroupCycle.Stack);

        Assert.That(X(effect, 0.4f, Steady), Is.EqualTo(100f).Within(0.01f));
        Assert.That(X(effect, 0.625f, Steady), Is.EqualTo(150f).Within(0.01f), "one done, half of the second");
        Assert.That(X(effect, 9f, Steady), Is.EqualTo(400f).Within(0.01f), "four steps in all");
    }

    [Test]
    public void CyclesKeepToTheBeatThroughABpmChange()
    {
        // 120 BPM, then 60 BPM from beat 2 (1 s): beats at 0, 0.5, 1, 2, 3 s.
        TempoMap tempo = TempoMap.From(new TempoPoint(0, 120), new[] { new TempoPoint(1_000, 60) });
        GroupEffect effect = Pulse(GroupCycle.Restart);

        Assert.That(effect.CycleSpan(3, tempo).Start, Is.EqualTo(2f).Within(0.001f), "the fourth beat is at 2 s");
        Assert.That(effect.CycleSpan(3, tempo).End, Is.EqualTo(2.5f).Within(0.001f), "half a beat long, at the slower tempo");
        Assert.That(X(effect, 2.25f, tempo), Is.EqualTo(50f).Within(0.01f));
    }

    [Test]
    public void TheLatestCycleWinsTheColour()
    {
        GroupDefinition[] groups = Slots(GroupEffect.Colour(0f, 0f, new Color(1, 0, 0)));
        groups[0].Effects.Add(GroupEffect.Colour(0.25f, 0.25f, new Color(0, 0, 1)));
        groups[0].Effects[0] = groups[0].Effects[0] with { Cycles = 4, EveryBeats = 1f };

        GroupModulation.TryColor(groups, ItemGroups.Bit(0), 0.3f, out Color early, out _, out _, Steady);
        GroupModulation.TryColor(groups, ItemGroups.Bit(0), 0.6f, out Color later, out _, out _, Steady);

        Assert.That(early.B, Is.EqualTo(1f).Within(0.01f), "the blue starts after the first red cycle");
        Assert.That(later.R, Is.EqualTo(1f).Within(0.01f), "the second red cycle starts after the blue and wins");
    }

    [Test]
    public void WithoutCyclesNothingChanges()
    {
        GroupEffect effect = GroupEffect.Move(0f, 1f, new Vector2(100, 0));

        Assert.That(X(effect, 0.5f, Steady), Is.EqualTo(50f).Within(0.01f));
        Assert.That(X(effect, 3f, Steady), Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void CyclesRoundTripThroughAMapAndAMapset()
    {
        var source = new LevelDocument { Name = "Cycles" };
        var group = new GroupDocument { Id = 2 };
        group.Effects.Add(new GroupEffectDocument { Kind = GroupEffectKind.Move, End = 0.25f });
        group.Effects.Add(new GroupEffectDocument { Kind = GroupEffectKind.Move, End = 0.25f, Cycles = 16, EveryBeats = 0.5f, Cycle = GroupCycle.Return });
        source.Groups.Add(group);

        GroupEffectDocument[] read = XmfBinary.Read(XmfBinary.Write(source)).Groups.Single().Effects.ToArray();
        Assert.That(read[0].Cycles, Is.EqualTo(1));
        Assert.That((read[1].Cycles, read[1].EveryBeats, read[1].Cycle), Is.EqualTo((16, 0.5f, GroupCycle.Return)));

        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var variant = new XmfVariant { Id = "hard", Name = "Hard" };
        variant.Groups.Add(group);
        mapset.Variants.Add(variant);
        GroupEffectDocument cycled = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset)).Variants.Single().Groups.Single().Effects[1];
        Assert.That((cycled.Cycles, cycled.Cycle), Is.EqualTo((16, GroupCycle.Return)));

        Assert.That(XmfBinary.Write(new LevelDocument { Name = "Plain" }).Length, Is.LessThan(XmfBinary.Write(source).Length));
    }
}
