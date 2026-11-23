using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// Motion paths: where a joint travels, sampled per frame, with its keys and
/// the turns sharp enough to flag. The fixture's head hangs 70 above the torso
/// and its rotation keys do not move its own joint, so the tests key the torso.
/// </summary>
[TestFixture]
public sealed class AnimatorMotionPathTests : AnimatorToolTest
{
    private const int Torso = 1;
    private const int Head = 2;

    private void KeyTorso(params (float Time, float Rotation)[] keys)
    {
        foreach ((float time, float rotation) in keys)
            RigAuthoring.SetBoneProperty(Package, Idle, "torso", RigChannelProperty.Rotation, time, rotation);
    }

    private AnimatorMotionPath HeadPath() =>
        AnimatorMotionPaths.Build(Package, Idle, new[] { Head })[0];

    [Test]
    public void APathHasAPointPerFrameAcrossTheClip()
    {
        AnimatorMotionPath path = HeadPath();

        Assert.That(path.Points, Has.Length.EqualTo(61), "2 s at 30 fps, both ends included");
        Assert.That(path.Times[0], Is.EqualTo(0f));
        Assert.That(path.Times[^1], Is.EqualTo(Idle.Duration).Within(0.0001f));
    }

    [Test]
    public void TheJointFollowsItsParentsRotation()
    {
        KeyTorso((0f, 0f), (2f, float.Pi / 2));

        AnimatorMotionPath path = HeadPath();

        Assert.That(path.Points[0].DistanceTo(path.Points[^1]), Is.GreaterThan(50f));
    }

    [Test]
    public void KeysOnTheBoneOrAnAncestorAreMarked()
    {
        KeyTorso((0f, 0f), (0.5f, 0.4f), (2f, 0f));

        AnimatorMotionPath path = HeadPath();

        // The head's own rotation keys (0, 1, 2 s) and the torso's (0, 0.5, 2 s).
        Assert.That(path.KeyTimes, Is.EqualTo(new[] { 0f, 0.5f, 1f, 2f }));
    }

    [Test]
    public void AnArcIsSmoothButASnapBackIsFlagged()
    {
        KeyTorso((0f, 0f), (1f, 0.8f), (1.0333f, -0.6f), (2f, -0.6f));

        AnimatorMotionPath path = HeadPath();
        float arc = 0f;
        for (int i = 1; i < 25; i++)
            arc = Math.Max(arc, path.Turn[i]);
        float snap = 0f;
        for (int i = 28; i < 34; i++)
            snap = Math.Max(snap, path.Turn[i]);

        Assert.That(arc, Is.LessThan(AnimatorMotionPaths.SharpDegrees));
        Assert.That(snap, Is.GreaterThanOrEqualTo(AnimatorMotionPaths.SharpDegrees));
    }

    [Test]
    public void TurnsAreMeasuredBetweenTicks()
    {
        Assert.That(
            AnimatorMotionPaths.TurnDegrees(new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0)),
            Is.EqualTo(0f).Within(0.01f));
        Assert.That(
            AnimatorMotionPaths.TurnDegrees(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10)),
            Is.EqualTo(90f).Within(0.01f));
        Assert.That(
            AnimatorMotionPaths.TurnDegrees(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 0)),
            Is.EqualTo(180f).Within(0.01f));
        Assert.That(
            AnimatorMotionPaths.TurnDegrees(new Vector2(0, 0), new Vector2(0.1f, 0), new Vector2(0.1f, 0.1f)),
            Is.EqualTo(0f),
            "a joint standing still has no direction to turn");
    }

    [Test]
    public void EditingFollowsTheSelectionAndPreviewTakesTheExtremities()
    {
        AnimatorSelection selection = AnimatorSelection.None;
        selection.SelectOnlyBone(Torso);

        Assert.That(AnimatorMotionPaths.Bones(Package, selection, false), Is.EqualTo(new List<int> { Torso }));
        Assert.That(
            AnimatorMotionPaths.Bones(Package, selection, true),
            Is.EqualTo(new List<int> { Head }),
            "the head is the fixture's only bone without children");
    }

    [Test]
    public void TheWindowKeepsHalfASecondEitherSideOfThePlayhead()
    {
        AnimatorMotionPath path = HeadPath();

        (int first, int last) = AnimatorMotionPaths.Span(path, true, 1f);

        Assert.That(path.Times[first], Is.EqualTo(0.5f).Within(AnimatorMotionPaths.Step));
        Assert.That(path.Times[last], Is.EqualTo(1.5f).Within(AnimatorMotionPaths.Step));
        Assert.That(AnimatorMotionPaths.Span(path, false, 1f), Is.EqualTo((0, path.Points.Length - 1)));
    }
}
