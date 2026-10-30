using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Bend locks: a locked knee is straightened when turned the wrong way, left
/// alone when bent its own way, IK folds it to its side, and the lock is saved.
/// The rig is one leg hanging from the hips: thigh, shin, foot.
/// </summary>
[TestFixture]
public sealed class XafJointLockTests
{
    private const int Thigh = 1;
    private const int Shin = 2;

    private static XafPackage Leg()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "thigh", new Vector2(0, -100));
        XafRigAuthoring.AddBone(package, Thigh, "shin", new Vector2(0, 50));
        XafRigAuthoring.AddBone(package, Shin, "foot", new Vector2(0, 50));
        return package;
    }

    private static float Bend(XafPackage package, XafPose pose)
    {
        XafJointLocks.TryBend(package, XafRig.WorldBones(package, pose), Shin, out float bend);
        return bend;
    }

    [Test]
    public void AKneeTurnedTheWrongWayIsStraightenedAndOneTurnedItsOwnWayIsNot()
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = 1;

        XafPose right = XafClothLayout.RestPose(package);
        right.Bones[Shin].Rotation += 0.8f;
        float before = Bend(package, right);
        Assert.That(XafJointLocks.Apply(package, right), Is.False);
        Assert.That(Bend(package, right), Is.EqualTo(before).Within(1e-4f));

        XafPose wrong = XafClothLayout.RestPose(package);
        wrong.Bones[Shin].Rotation -= 0.8f;
        Assert.That(Bend(package, wrong), Is.LessThan(0f));
        Assert.That(XafJointLocks.Apply(package, wrong), Is.True);
        Assert.That(Bend(package, wrong), Is.EqualTo(0f).Within(1e-4f), "stops at straight");
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void IkFoldsALockedKneeToItsSide(int locked)
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = locked;
        Transform2D[] world = XafRig.WorldBones(package, XafClothLayout.RestPose(package));
        Vector2[] chain = { world[Thigh].Origin, world[Shin].Origin, world[3].Origin };

        Vector2[] solved = XafFabrik.Solve(chain, chain[0] + new Vector2(30, 60),
            XafJointLocks.BendPositive(package, Shin)!.Value);

        float bend = (solved[1] - solved[0]).Cross(solved[2] - solved[1]);
        Assert.That(Mathf.Sign(bend), Is.EqualTo(locked));
    }

    [Test]
    public void LocksAreSavedAndOlderFilesHaveNone()
    {
        XafPackage package = Leg();
        package.Skeleton.Bones[Shin].Bend = -1;

        Assert.That(XafBinary.Read(XafBinary.Write(package)).Skeleton.Bones[Shin].Bend, Is.EqualTo(-1));
        Assert.That(XafBinary.Read(XafBinary.Write(package, 7)).Skeleton.Bones[Shin].Bend, Is.Zero);
        Assert.That(XafPackageCopy.AreEqual(package, XafPackageCopy.Clone(package)), Is.True);
    }
}
