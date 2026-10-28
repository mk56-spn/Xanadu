using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorAnalysisTests
{
    // A 1000px field drawn 1:1 at the origin, with the real player and note sizes.
    private static readonly PlayabilityGeometry Field = new(
        Vector2.Zero,
        new Vector2(1000, 1000),
        1f,
        Player.DefaultSize,
        72,
        3000f,
        150f);

    private static AnalysedNote Hit(long time, float x, float y) =>
        new(time, new Vector2(x, y), NoteKind.Hit, DashDirection.Up);

    private static AnalysedNote Dash(long time, float x, float y, DashDirection direction) =>
        new(time, new Vector2(x, y), NoteKind.Movement, direction);

    private static List<PlayabilityIssue> Analyze(Vector2? start = null, params AnalysedNote[] notes) =>
        PlayabilityAnalyzer.Analyze(notes, Field, start);

    [Test]
    public void ANoteAtTheStartingSpotIsReachable() =>
        Assert.That(Analyze(null, Hit(1000, 500, 900)), Is.Empty);

    [Test]
    public void ANoteFarToTheSideCannotBeReached()
    {
        List<PlayabilityIssue> issues = Analyze(null, Hit(1000, 100, 900));

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.That(issues[0].Problem, Is.EqualTo(PlayabilityProblem.TooFarSideways));
        Assert.That(issues[0].TimeMs, Is.EqualTo(1000));
    }

    [Test]
    public void ANoteJustOffThePlayersThinBoxIsStillReachable()
    {
        // 60 px sideways: outside the box's reach, inside the note-reach sphere.
        Assert.That(Analyze(null, Hit(1000, 560, 900)), Is.Empty);
    }

    [Test]
    public void ANoteAboveWhereGravityLetsThePlayerBeCannotBeReached()
    {
        List<PlayabilityIssue> issues = Analyze(null, Hit(1000, 500, 100));

        Assert.That(issues.Single().Problem, Is.EqualTo(PlayabilityProblem.TooHigh));
    }

    [Test]
    public void TheFieldWrapsSidewaysSoTheFarEdgeIsClose()
    {
        // Starting at the left edge, a note at the right edge is 40px away round the back.
        Assert.That(Analyze(new Vector2(30, 900), Hit(1000, 990, 900)), Is.Empty);
    }

    [Test]
    public void ADashMovesThePlayerToItsTarget()
    {
        // Dash up from the floor, then hit something above the original floor spot.
        AnalysedNote[] chart =
        [
            Dash(1000, 500, 895, DashDirection.Up),
            Hit(1400, 500, 700),
        ];

        Assert.That(PlayabilityAnalyzer.Analyze(chart, Field), Is.Empty);
    }

    [Test]
    public void AfterADashTheNextNoteStillHasToBeWithinReachOfTheLandingSpot()
    {
        AnalysedNote[] chart =
        [
            Dash(1000, 500, 895, DashDirection.Up),
            Hit(1400, 500, 500),
        ];

        Assert.That(
            PlayabilityAnalyzer.Analyze(chart, Field).Single().Problem,
            Is.EqualTo(PlayabilityProblem.TooHigh));
    }

    [Test]
    public void ALongerGapLetsGravityBringThePlayerDownToANote()
    {
        // The same height is reachable if the player has had time to drop.
        AnalysedNote[] chart =
        [
            Dash(1000, 500, 895, DashDirection.Up),
            Hit(3000, 500, 900),
        ];

        Assert.That(PlayabilityAnalyzer.Analyze(chart, Field), Is.Empty);
    }

    [Test]
    public void AnUnreachableDashStillMovesThePlayerSoErrorsDoNotCascade()
    {
        AnalysedNote[] chart =
        [
            Dash(1000, 100, 895, DashDirection.Right),
            Hit(1600, 250, 880),
            Hit(1800, 250, 880),
        ];

        List<PlayabilityIssue> issues = PlayabilityAnalyzer.Analyze(chart, Field);

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.That(issues[0].TimeMs, Is.EqualTo(1000));
    }

    [Test]
    public void AHitAtTheSameInstantAsADashSettlesBeforeTheDash()
    {
        AnalysedNote[] chart =
        [
            Dash(1000, 500, 895, DashDirection.Right),
            Hit(1000, 500, 900),
        ];

        Assert.That(PlayabilityAnalyzer.Analyze(chart, Field), Is.Empty);
    }

    [Test]
    public void ReachScalesWithTheViewSoAHalfSizeFieldIsJudgedInScreenPixels()
    {
        PlayabilityGeometry half = Field with
        {
            Origin = new Vector2(100, 50),
            FieldSize = new Vector2(500, 500),
            Scale = 0.5f,
            NoteSize = 36,
        };

        // Same logical spot as the start, so reachable at any scale.
        Assert.That(PlayabilityAnalyzer.Analyze([Hit(1000, 500, 900)], half), Is.Empty);
        Assert.That(PlayabilityAnalyzer.Analyze([Hit(1000, 100, 900)], half), Has.Count.EqualTo(1));
    }

    [Test]
    public void EmptyChartHasNoIssues() =>
        Assert.That(PlayabilityAnalyzer.Analyze([], Field), Is.Empty);

    [Test]
    public void DensityCountsNotesPerSecond()
    {
        // Four notes a second for ten seconds, then silence.
        List<AnalysedNote> notes = Enumerable.Range(0, 40)
            .Select(i => Hit(i * 250, 500, 500))
            .ToList();

        DensityProfile profile = LevelDensity.Compute(notes, 14_000);

        Assert.That(profile.NotesPerSecond[10], Is.EqualTo(4f).Within(0.01f));
        Assert.That(profile.NotesPerSecond[^1], Is.EqualTo(0f));
        Assert.That(profile.PeakNotes, Is.EqualTo(4f).Within(0.01f));
    }

    [Test]
    public void DensityUnsortedInputGivesTheSameProfile()
    {
        List<AnalysedNote> notes = Enumerable.Range(0, 20).Select(i => Hit(i * 300, i * 10, 0)).ToList();

        DensityProfile sorted = LevelDensity.Compute(notes, 7000);
        notes.Reverse();
        DensityProfile reversed = LevelDensity.Compute(notes, 7000);

        Assert.That(reversed.NotesPerSecond, Is.EqualTo(sorted.NotesPerSecond));
        Assert.That(reversed.TravelPixelsPerSecond, Is.EqualTo(sorted.TravelPixelsPerSecond));
    }

    [Test]
    public void DensityMeasuresTravelBetweenNotes()
    {
        // 3-4-5: two notes 500px apart, the second in bucket 2.
        AnalysedNote[] notes = [Hit(0, 0, 0), Hit(1000, 300, 400)];

        DensityProfile profile = LevelDensity.Compute(notes, 2000);

        Assert.That(profile.TravelPixelsPerSecond[0], Is.EqualTo(0f).Within(0.01f));
        Assert.That(profile.PeakTravel, Is.GreaterThan(0f));
        Assert.That(profile.TravelPixelsPerSecond.Sum() * profile.BucketMs / 1000f, Is.EqualTo(500f).Within(1f));
    }

    [Test]
    public void DensityOfNothingIsEmpty()
    {
        DensityProfile profile = LevelDensity.Compute([], 0);

        Assert.That(profile.BucketCount, Is.EqualTo(0));
        Assert.That(profile.PeakNotes, Is.EqualTo(0f));
    }

    [Test]
    public void DensityCoversNotesPastTheSongDuration()
    {
        DensityProfile profile = LevelDensity.Compute([Hit(9000, 0, 0)], 1000);

        Assert.That(profile.BucketCount, Is.GreaterThan(17));
    }
}
