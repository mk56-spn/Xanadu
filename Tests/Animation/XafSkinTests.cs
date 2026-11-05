using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Skins: one mesh over a whole limb. Only the cells the art covers are drawn,
/// a point's bones are found by travelling through the art rather than across
/// gaps in it, a bone only carries what lies past its joint, a bone with no
/// child runs to its tip, and a joint brought out of the plane squashes the
/// skin either side of it.
/// </summary>
[TestFixture]
public sealed class XafSkinTests
{
    private static XafMesh Mesh(int detail) => new() { Detail = detail, Blend = 10f };

    [Test]
    public void OnlyTheCellsTheArtCoversGetTriangles()
    {
        XafMesh mesh = Mesh(4);
        var size = new Vector2(40, 40);

        // A disc: the grid's corner cells lie outside it.
        bool[] covered = XafSkin.Covered(mesh, size, uv => uv.DistanceTo(new Vector2(0.5f, 0.5f)) < 0.3f ? 1f : 0f);
        int[] triangles = XafSkin.Triangles(mesh, size, covered);

        Assert.That(covered, Has.Length.EqualTo(16));
        Assert.That(covered[0] || covered[3] || covered[12] || covered[15], Is.False, "the corners are empty");
        Assert.That(covered[5] && covered[6] && covered[9] && covered[10], Is.True, "the middle is art");
        Assert.That(triangles, Has.Length.EqualTo(covered.Count(cell => cell) * 6));
        Assert.That(XafSkin.Triangles(mesh, size, null), Is.EqualTo(XafSkin.Triangles(mesh, size)));
    }

    [Test]
    public void APointFollowsOnlyTheBonesItCanReachThroughTheArt()
    {
        // Two legs, ten either side of the middle, drawn as two strips with a gap between.
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        int left = XafRigAuthoring.AddBone(package, 0, "left", new Vector2(-10, 0));
        XafRigAuthoring.AddBone(package, left, "left_end", new Vector2(0, 100));
        int right = XafRigAuthoring.AddBone(package, 0, "right", new Vector2(10, 0));
        XafRigAuthoring.AddBone(package, right, "right_end", new Vector2(0, 100));
        Transform2D[] bones = XafRig.WorldBones(package, XafClothLayout.RestPose(package));

        // Detail 20 puts a point every 5 across and down.
        XafMesh mesh = Mesh(20);
        var art = new Rect2(-20, 0, 40, 100);
        (Vector2[] points, _) = XafSkin.Grid(mesh, art);
        (int columns, int rows) = XafSkin.Points(mesh, art.Size);
        bool[] covered = XafSkin.Covered(mesh, art.Size, uv => MathF.Abs(-20 + 40 * uv.X) > 5.2f ? 1f : 0f);
        var influences = new List<int> { left, right };

        // The left strip's inner edge, half way down: nearer the left bone, but not by much.
        int inner = Array.FindIndex(points, point => point.IsEqualApprox(new Vector2(-5, 50)));
        Assert.That(inner, Is.GreaterThanOrEqualTo(0));

        float[] across = XafSkin.Weights(package, influences, points, bones, 10f)[inner];
        float[] through = XafSkin.Weights(package, influences, points, bones, 10f, columns, rows, covered)[inner];

        Assert.That(across[1], Is.GreaterThan(0.1f), "measured across the gap, the other leg pulls on it");
        Assert.That(through[0], Is.EqualTo(1f), "measured through the art, only its own leg does");
    }

    /// <summary>A leg hanging from the origin, 50 to the knee and 50 to the ankle, its foot pointing 30 to the right.</summary>
    private static (XafPackage Package, int Thigh, int Shin, int Foot) Leg()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        int thigh = XafRigAuthoring.AddBone(package, 0, "thigh", Vector2.Zero);
        int shin = XafRigAuthoring.AddBone(package, thigh, "shin", new Vector2(0, 50));
        int foot = XafRigAuthoring.AddBone(package, shin, "foot", new Vector2(0, 50));
        package.Skeleton.Bones[foot].Tip = new Vector2(30, 0);
        return (package, thigh, shin, foot);
    }

    [Test]
    public void ABoneWithNoChildRunsToItsTip()
    {
        var (package, _, _, foot) = Leg();
        Transform2D[] bones = XafRig.WorldBones(package, XafClothLayout.RestPose(package));

        Assert.That(XafSkin.Segment(package, bones, foot), Is.EqualTo((new Vector2(0, 100), new Vector2(30, 100))));
    }

    [Test]
    public void AFootTakesItsToesButNotTheShinBesideIt()
    {
        var (package, _, shin, foot) = Leg();
        Transform2D[] bones = XafRig.WorldBones(package, XafClothLayout.RestPose(package));
        var influences = new List<int> { shin, foot };
        Vector2[] points = { new(4, 88), new(24, 101) };

        float[][] weights = XafSkin.Weights(package, influences, points, bones, 10f);

        Assert.That(weights[0][0], Is.GreaterThan(0.75f), "just above the ankle is shin, though the foot runs right past it");
        Assert.That(weights[1][1], Is.GreaterThan(0.9f), "the toes are foot");
    }

    [Test]
    public void AKneeOutOfThePlaneSquashesTheSkinOnBothSides()
    {
        var (package, thigh, shin, foot) = Leg();
        XafPose rest = XafClothLayout.RestPose(package);
        Transform2D[] restBones = XafRig.WorldBones(package, rest);
        XafPose pose = rest.Clone();
        pose.Bones[shin].Depth = 30f;
        Transform2D[] bones = XafRig.WorldBones(package, pose);
        Transform2D[] squash = Enumerable.Range(0, bones.Length)
            .Select(bone => XafRig.Foreshorten(package, pose, bone)).ToArray();

        // Half way down the thigh and half way down the shin, each all on its own bone.
        var influences = new List<int> { thigh, shin, foot };
        Vector2[] points = { new(0, 25), new(0, 75) };
        float[][] weights = { new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f } };
        Vector2[] bent = XafSkin.Deform(influences, weights, points, restBones, bones,
            restBones[thigh], bones[thigh] * squash[thigh], squash);

        // Each 50 long segment shows 40 of its length.
        Assert.That(bent[0].Y, Is.EqualTo(20f).Within(1e-3f));
        Assert.That(bent[1].Y, Is.EqualTo(60f).Within(1e-3f));
    }

    [Test]
    public void TipsAreSavedAndOlderFilesHaveNone()
    {
        var (package, _, _, foot) = Leg();

        Assert.That(XafBinary.Read(XafBinary.Write(package)).Skeleton.Bones[foot].Tip, Is.EqualTo(new Vector2(30, 0)));
        Assert.That(XafBinary.Read(XafBinary.Write(package, 13)).Skeleton.Bones[foot].Tip, Is.EqualTo(Vector2.Zero));
        Assert.That(XafPackageCopy.AreEqual(package, XafPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void APointSharedAcrossABentKneeSwingsRoundItRatherThanCuttingAcross()
    {
        var (package, thigh, shin, foot) = Leg();
        XafPose rest = XafClothLayout.RestPose(package);
        Transform2D[] restBones = XafRig.WorldBones(package, rest);
        XafPose pose = rest.Clone();
        pose.Bones[shin].Rotation = Mathf.Pi / 2f;
        Transform2D[] bones = XafRig.WorldBones(package, pose);

        // Five behind the knee, half thigh and half shin.
        var knee = new Vector2(0, 50);
        Vector2[] points = { knee + new Vector2(-5, 0) };
        float[][] weights = { new[] { 0.5f, 0.5f, 0f } };
        Vector2 bent = XafSkin.Deform(new List<int> { thigh, shin, foot }, weights, points, restBones, bones,
            restBones[thigh], bones[thigh])[0];

        // Mixing the two end points would pull it in to 3.5 from the knee.
        Assert.That(bent.DistanceTo(knee), Is.EqualTo(5f).Within(1e-3f));
    }
}
