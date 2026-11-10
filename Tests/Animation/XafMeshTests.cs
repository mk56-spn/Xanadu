using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Meshed attachments: a thigh's art over a leg that bends at the knee. Its
/// points follow the thigh, the shin, or both near the knee; at rest nothing
/// moves, and bending the knee bends the art's lower end with the shin.
/// </summary>
[TestFixture]
public sealed class XafMeshTests
{
    private const int Thigh = 1;
    private const int Shin = 2;

    private static XafPackage Leg()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "thigh", new Vector2(0, -100));
        XafRigAuthoring.AddBone(package, Thigh, "shin", new Vector2(0, 50));
        XafRigAuthoring.AddBone(package, Shin, "foot", new Vector2(0, 50));
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://leg.svg", Group = "thigh", BoneId = "thigh",
            Transform = new Transform2D(0f, new Vector2(0, 25)),
            Mesh = new XafMesh { Detail = 8, Blend = 10f },
        });
        return package;
    }

    private static (Vector2[] Points, List<int> Influences, float[][] Weights, Transform2D[] RestBones, Transform2D RestAttachment)
        Bind(XafPackage package)
    {
        XafPose rest = XafClothLayout.RestPose(package);
        Transform2D[] bones = XafRig.WorldBones(package, rest);
        Transform2D attachment = XafRig.WorldAttachments(package, rest, bones)[0];
        (Vector2[] grid, _) = XafSkinGrid.Grid(package.Attachments[0].Mesh!, new Rect2(-8, -30, 16, 60));
        Vector2[] points = grid.Select(point => attachment * point).ToArray();
        List<int> influences = XafSkinWeights.Influences(package, package.Attachments[0]);
        return (points, influences, XafSkinWeights.Weights(package, influences, points, bones, 10f), bones, attachment);
    }

    [Test]
    public void ALimbSegmentFollowsItsOwnBoneAndTheOneBelowWithWeightsThatAddUp()
    {
        XafPackage package = Leg();
        var bind = Bind(package);

        Assert.That(bind.Influences, Is.EqualTo(new[] { Thigh, Shin }));
        foreach (float[] row in bind.Weights)
            Assert.That(row.Sum(), Is.EqualTo(1f).Within(1e-4f));

        int top = 0;
        int bottom = bind.Points.Length - 1;
        Assert.That(bind.Weights[top][0], Is.GreaterThan(0.99f), "the hip end is all thigh");
        Assert.That(bind.Weights[bottom][1], Is.GreaterThan(0.4f), "the knee end follows the shin too");
    }

    [Test]
    public void AtRestNothingMovesAndBendingTheKneeBendsTheLowerEnd()
    {
        XafPackage package = Leg();
        var bind = Bind(package);
        Vector2[] still = XafSkinDeform.Deform(bind.Influences, bind.Weights, bind.Points, bind.RestBones, bind.RestBones,
            bind.RestAttachment, bind.RestAttachment);
        for (int i = 0; i < still.Length; i++)
            Assert.That(still[i].DistanceTo(bind.Points[i]), Is.LessThan(1e-3f));

        XafPose pose = XafClothLayout.RestPose(package);
        pose.Bones[Shin].Rotation = 1.2f;
        Transform2D[] bones = XafRig.WorldBones(package, pose);
        Transform2D attachment = XafRig.WorldAttachments(package, pose, bones)[0];
        Vector2[] bent = XafSkinDeform.Deform(bind.Influences, bind.Weights, bind.Points, bind.RestBones, bones,
            bind.RestAttachment, attachment);

        Assert.That(bent[0].DistanceTo(bind.Points[0]), Is.LessThan(1e-3f), "the hip end stays");
        Assert.That(bent[^1].DistanceTo(bind.Points[^1]), Is.GreaterThan(5f), "the knee end swings with the shin");
    }

    [Test]
    public void MeshesAreSavedAndTheirBonesCannotBeDeleted()
    {
        XafPackage package = Leg();
        package.Attachments[0].Mesh!.Bones.Add("foot");

        Assert.That(XafPackageCopy.AreEqual(package, XafBinary.Read(XafBinary.Write(package))), Is.True);
        Assert.That(XafRigAuthoring.DeleteBone(package, 3), Is.False, "the foot bends the mesh");
        XafRigAuthoring.RenameBone(package, 3, "toe");
        Assert.That(package.Attachments[0].Mesh!.Bones, Does.Contain("toe"));
    }
}
