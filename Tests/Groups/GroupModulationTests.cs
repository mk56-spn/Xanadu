using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Groups;

[TestFixture]
public sealed class GroupModulationTests
{
    private static GroupDefinition[] Slots()
    {
        var groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        return groups;
    }

    private static uint In(params int[] groups) => ItemGroups.FromList(groups);

    [Test]
    public void AMoveGlidesFromItsStartToItsEndThenHoldsTheOffset()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(0.4f, 0.6f, new Vector2(100, 40)));

        Assert.That(GroupModulation.Offset(groups, In(0), 0.3f), Is.EqualTo(Vector2.Zero));
        Assert.That(GroupModulation.Offset(groups, In(0), 0.5f).X, Is.EqualTo(50f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(0), 0.5f).Y, Is.EqualTo(20f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(0), 0.6f), Is.EqualTo(new Vector2(100, 40)));
        Assert.That(GroupModulation.Offset(groups, In(0), 5f), Is.EqualTo(new Vector2(100, 40)));
    }

    [Test]
    public void AMoveCanEndLongBeforeTheNextOneStarts()
    {
        // Move at 0.4 to 0.6; the next move only begins at 0.8. In between, the first holds.
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(0.4f, 0.6f, new Vector2(100, 0)));
        groups[0].Effects.Add(GroupEffect.Move(0.8f, 1.0f, new Vector2(0, 50)));

        Assert.That(GroupModulation.Offset(groups, In(0), 0.7f), Is.EqualTo(new Vector2(100, 0)));
        Assert.That(GroupModulation.Offset(groups, In(0), 0.8f), Is.EqualTo(new Vector2(100, 0)));
        Assert.That(GroupModulation.Offset(groups, In(0), 0.9f).Y, Is.EqualTo(25f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(0), 1.5f), Is.EqualTo(new Vector2(100, 50)));
    }

    [Test]
    public void MovesFromEveryGroupAnItemIsInAreAddedTogether()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0)));
        groups[3].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(0, 60)));

        Assert.That(GroupModulation.Offset(groups, In(0, 3), 1f), Is.EqualTo(new Vector2(100, 60)));
        Assert.That(GroupModulation.Offset(groups, In(0), 1f), Is.EqualTo(new Vector2(100, 0)));
        Assert.That(GroupModulation.Offset(groups, In(3), 1f), Is.EqualTo(new Vector2(0, 60)));
        Assert.That(GroupModulation.Offset(groups, In(5), 1f), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ADifferentGroupsMoveNeverAffectsItemsOutsideIt()
    {
        GroupDefinition[] groups = Slots();
        groups[1].Effects.Add(GroupEffect.Move(0f, 0.1f, new Vector2(9, 9)));

        Assert.That(GroupModulation.Offset(groups, 0u, 10f), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void AnInstantMoveJumpsAtItsStart()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(1f, 1f, new Vector2(30, 0)));

        Assert.That(GroupModulation.Offset(groups, In(0), 0.99f), Is.EqualTo(Vector2.Zero));
        Assert.That(GroupModulation.Offset(groups, In(0), 1f), Is.EqualTo(new Vector2(30, 0)));
    }

    [Test]
    public void AColourFadesInOverItsSpanThenStays()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(1f, 2f, Colors.Red));

        Assert.That(GroupModulation.TryColor(groups, In(0), 0.5f, out _, out _), Is.False);

        Assert.That(GroupModulation.TryColor(groups, In(0), 1.5f, out Color mid, out float coverage), Is.True);
        Assert.That(coverage, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(mid.R, Is.EqualTo(1f).Within(0.001f));

        Assert.That(GroupModulation.TryColor(groups, In(0), 9f, out Color done, out float full), Is.True);
        Assert.That(full, Is.EqualTo(1f).Within(0.001f));
        Assert.That(done, Is.EqualTo(Colors.Red));
    }

    [Test]
    public void TheColourThatStartedLatestWins()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(1f, 1.1f, Colors.Red));
        groups[1].Effects.Add(GroupEffect.Colour(2f, 2.1f, Colors.Blue));

        GroupModulation.TryColor(groups, In(0, 1), 5f, out Color both, out _);
        Assert.That(both.B, Is.EqualTo(1f).Within(0.001f));
        Assert.That(both.R, Is.EqualTo(0f).Within(0.001f));

        // Before the second starts, only the first is showing.
        GroupModulation.TryColor(groups, In(0, 1), 1.5f, out Color first, out _);
        Assert.That(first.R, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void TheLatestStartWinsWhicheverGroupHoldsIt()
    {
        // The earlier-numbered group starts later, so it wins over the later-numbered one.
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(3f, 3.1f, Colors.Green));
        groups[7].Effects.Add(GroupEffect.Colour(1f, 1.1f, Colors.Red));

        GroupModulation.TryColor(groups, In(0, 7), 10f, out Color mix, out _);

        Assert.That(mix.G, Is.EqualTo(1f).Within(0.001f));
        Assert.That(mix.R, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ALaterColourFadesFromWhatTheEarlierOneLeft()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(0f, 0.1f, Colors.Red));
        groups[0].Effects.Add(GroupEffect.Colour(1f, 2f, Colors.Blue));

        GroupModulation.TryColor(groups, In(0), 1.5f, out Color mix, out float coverage);

        Assert.That(coverage, Is.EqualTo(1f).Within(0.001f));
        Assert.That(mix.R, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(mix.B, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void AColourMayEndBeforeAnotherStartsAndStillHolds()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(0.4f, 0.6f, Colors.Red));
        groups[1].Effects.Add(GroupEffect.Colour(0.8f, 0.9f, Colors.Blue));

        GroupModulation.TryColor(groups, In(0, 1), 0.7f, out Color mix, out float coverage);

        Assert.That(coverage, Is.EqualTo(1f).Within(0.001f));
        Assert.That(mix, Is.EqualTo(Colors.Red));
    }

    [Test]
    public void ColourAlphaFadesAnItemOut()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(0f, 1f, new Color(1f, 1f, 1f, 0f)));

        GroupModulation.TryColor(groups, In(0), 1f, out Color mix, out float coverage);
        Color drawn = new GroupColor(mix, coverage).Apply(new Color(0.2f, 0.4f, 0.6f, 0.8f));

        Assert.That(drawn.A, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ApplyingNoCoverageLeavesTheItemsColour()
    {
        Color basis = new(0.2f, 0.4f, 0.6f, 0.8f);

        Assert.That(new GroupColor(Colors.Red, 0f).Apply(basis), Is.EqualTo(basis));
    }

    [Test]
    public void MembershipBitsRoundTripThroughLists()
    {
        uint mask = ItemGroups.FromList(new[] { 0, 5, 15, 99, -1 });

        Assert.That(ItemGroups.ToList(mask), Is.EqualTo(new List<int> { 0, 5, 15 }));
        Assert.That(new ItemGroups(mask).Has(5), Is.True);
        Assert.That(new ItemGroups(mask).Has(6), Is.False);
    }

    [Test]
    public void AnEasedMoveKeepsItsEndsAndBendsTheMiddle()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0), GroupEase.Linear));
        groups[1].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0), GroupEase.In));
        groups[2].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0), GroupEase.Out));
        groups[3].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(100, 0), GroupEase.InOut));

        Assert.That(GroupModulation.Offset(groups, In(0), 0f).X, Is.EqualTo(0f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(0), 1f).X, Is.EqualTo(100f).Within(0.001f));

        Assert.That(GroupModulation.Offset(groups, In(0), 0.5f).X, Is.EqualTo(50f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(1), 0.5f).X, Is.EqualTo(25f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(2), 0.5f).X, Is.EqualTo(75f).Within(0.001f));
        Assert.That(GroupModulation.Offset(groups, In(3), 0.5f).X, Is.EqualTo(50f).Within(0.001f));

        // The curve still holds the full offset after the effect ends.
        Assert.That(GroupModulation.Offset(groups, In(1), 2f).X, Is.EqualTo(100f).Within(0.001f));
    }

    [Test]
    public void EasingCyclesThroughEveryCurve()
    {
        Assert.That(GroupEaseCurves.Next(GroupEase.Linear), Is.EqualTo(GroupEase.In));
        Assert.That(GroupEaseCurves.Next(GroupEase.In), Is.EqualTo(GroupEase.Out));
        Assert.That(GroupEaseCurves.Next(GroupEase.Out), Is.EqualTo(GroupEase.InOut));
        Assert.That(GroupEaseCurves.Next(GroupEase.InOut), Is.EqualTo(GroupEase.Linear));

        Assert.That(GroupEaseCurves.Apply(GroupEase.Linear, -1f), Is.EqualTo(0f));
        Assert.That(GroupEaseCurves.Apply(GroupEase.Linear, 2f), Is.EqualTo(1f));
    }

    [Test]
    public void AColourUsesItsEasingCurveToo()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Colour(0f, 1f, Colors.Red, GroupEase.Out));

        GroupModulation.TryColor(groups, In(0), 0.5f, out _, out float coverage);

        // Ease-out is three quarters of the way through at the halfway point.
        Assert.That(coverage, Is.EqualTo(0.75f).Within(0.001f));
    }

    [Test]
    public void TargetHandleRoundsAndCentres()
    {
        Assert.That(
            EditorGroupTarget.Tidy(new Vector2(10.24f, -3.76f)),
            Is.EqualTo(new Vector2(10f, -4f)));

        Rect2 handle = EditorGroupTarget.HandleRect(new Vector2(100, 100));
        Assert.That(handle.GetCenter(), Is.EqualTo(new Vector2(100, 100)));
        Assert.That(handle.Size, Is.EqualTo(new Vector2(28, 28)));
    }

    [Test]
    public void MutedMovesAndColoursAreIgnored()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Effects.Add(GroupEffect.Move(
            0f, 1f, new Vector2(100, 0), GroupEase.Linear, muted: true));
        groups[1].Effects.Add(GroupEffect.Colour(
            0f, 1f, Colors.Red, GroupEase.Linear, muted: true));

        Assert.That(GroupModulation.Offset(groups, In(0, 1), 10f), Is.EqualTo(Vector2.Zero));
        Assert.That(GroupModulation.TryColor(groups, In(0, 1), 10f, out _, out _), Is.False);
    }

    [Test]
    public void AGroupedStartPointSpawnsAtItsModulatedPosition()
    {
        LevelGroups groups = LevelGroups.Create();
        groups.Groups[0].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(300, 0)));
        var start = new StartPoint(
            "Intro",
            500,
            new Vector2(100, 100),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1),
            ItemGroups.Bit(0));

        Assert.That(
            PlayScene.SpawnPosition(groups, start),
            Is.EqualTo(new Vector2(250, 100)));
    }
}
