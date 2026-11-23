using Xanadu.Serialisation.Animation;
using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Cloth: the default dress, how it hangs and follows the body, how its edges
/// stay out of the legs, and that it survives a save, a copy and a bone rename.
/// The rig is a small standing figure: hips 100 up, a torso rising from them,
/// and one leg down to a foot on the ground.
/// </summary>
[TestFixture]
public sealed class RigClothTests
{
    private const int Torso = 1;
    private const int Thigh = 3;

    private static RigPackage Rig()
    {
        RigPackage package = RigRigAuthoring.CreateEmptyPackage();
        RigRigAuthoring.AddBone(package, 0, "torso", new Vector2(0, -100));
        RigRigAuthoring.AddBone(package, Torso, "head", new Vector2(0, -70));
        RigRigAuthoring.AddBone(package, Torso, "thigh", new Vector2(4, 0));
        RigRigAuthoring.AddBone(package, Thigh, "shin", new Vector2(0, 50));
        RigRigAuthoring.AddBone(package, 4, "foot", new Vector2(0, 50));
        return package;
    }

    private static RigPackage Dressed()
    {
        RigPackage package = Rig();
        RigClothAuthoring.Add(package, Torso);
        return package;
    }

    private static Transform2D[] Rest(RigPackage package) =>
        RigRig.WorldBones(package, RigClothLayout.RestPose(package));

    /// <summary>Runs the simulation for a while with the bones held in one pose.</summary>
    private static IReadOnlyList<Vector2> Hang(
        RigPackage package,
        RigClothSimulation simulation,
        Transform2D[] world,
        float seconds = 2f)
    {
        for (float t = 0; t < seconds; t += RigClothSimulation.FixedStep)
            simulation.Update(package, world, RigClothSimulation.FixedStep);
        return simulation.Points(0);
    }

    [Test]
    public void ADressIsPinnedDownToTheWaistAndDrapesOverTheLegs()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        Vector2[] rest = RigClothLayout.RestPoints(package, dress, Rest(package));
        float waist = Rest(package)[Torso].Origin.Y;

        Assert.That(dress.BoneId, Is.EqualTo("torso"));
        Assert.That(dress.Pins, Is.Not.Empty);
        foreach (RigClothPin pin in dress.Pins)
            Assert.That(rest[dress.Index(pin.Column, pin.Row)].Y, Is.LessThanOrEqualTo(waist + 0.5f));
        Assert.That(dress.IsPinned(0, dress.Rows - 1), Is.False, "the hem is free");

        List<string> colliders = dress.Colliders.ConvertAll(collider => collider.BoneId);
        Assert.That(colliders, Does.Contain("thigh").And.Contain("shin"));
        Assert.That(colliders, Does.Not.Contain("head").And.Not.Contain("root"));
    }

    [Test]
    public void AtRestItHangsAsLaidOut()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        Transform2D[] rest = Rest(package);
        Vector2[] laidOut = RigClothLayout.RestPoints(package, dress, rest);

        IReadOnlyList<Vector2> points = Hang(package, new RigClothSimulation(), rest);

        Assert.That(points, Has.Count.EqualTo(dress.Count));
        Vector2 hem = points[dress.Index(dress.Columns / 2, dress.Rows - 1)];
        Assert.That(
            hem.Y,
            Is.EqualTo(laidOut[dress.Index(dress.Columns / 2, dress.Rows - 1)].Y).Within(3f),
            "it neither stretches nor rides up");
    }

    [Test]
    public void PinnedPointsRideTheirBone()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[Torso].Rotation = 0.5f;
        Transform2D[] turned = RigRig.WorldBones(package, pose);
        Vector2[] laidOut = RigClothLayout.RestPoints(package, dress, Rest(package));

        IReadOnlyList<Vector2> points = Hang(package, new RigClothSimulation(), turned, 0.5f);

        int top = dress.Index(0, 0);
        Vector2 expected = turned[Torso] * (Rest(package)[Torso].AffineInverse() * laidOut[top]);
        Assert.That(points[top].DistanceTo(expected), Is.LessThan(0.01f));
    }

    [Test]
    public void TheSkirtSwingsWhenTheBodyMoves()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        var simulation = new RigClothSimulation();
        Transform2D[] rest = Rest(package);
        Hang(package, simulation, rest);
        Vector2 before = simulation.Points(0)[dress.Index(dress.Columns / 2, dress.Rows - 1)];

        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[0].Translation = new Vector2(60, 0);
        simulation.Update(package, RigRig.WorldBones(package, pose), 0.1f);
        Vector2 after = simulation.Points(0)[dress.Index(dress.Columns / 2, dress.Rows - 1)];

        Assert.That(after.X - before.X, Is.GreaterThan(0f).And.LessThan(60f), "the hem trails the body");
    }

    [Test]
    public void AKickingLegPushesTheSkirtsEdgeOut()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        foreach (RigClothCollider collider in dress.Colliders)
            collider.Radius = 6f;
        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[Thigh].Rotation = -1.4f;
        Transform2D[] kicked = RigRig.WorldBones(package, pose);

        IReadOnlyList<Vector2> points = Hang(package, new RigClothSimulation(), kicked);

        (Vector2 From, Vector2 To) thigh = RigClothLayout.Capsule(
            package,
            new RigClothCollider { BoneId = "thigh" },
            kicked)!.Value;
        for (int row = 0; row < dress.Rows; row++)
        {
            foreach (int column in new[] { 0, dress.Columns - 1 })
            {
                Vector2 point = points[dress.Index(column, row)];
                float distance = RigClothLayout.ClosestOnSegment(point, thigh.From, thigh.To).DistanceTo(point);
                if (!dress.IsPinned(column, row))
                    Assert.That(
                        distance,
                        Is.GreaterThanOrEqualTo(6f - 0.5f),
                        $"edge point {column},{row} is outside the leg");
            }
        }
    }

    [Test]
    public void AKickFasterThanAFrameStillLiftsTheSkirt()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        var simulation = new RigClothSimulation();
        Hang(package, simulation, Rest(package));

        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[Thigh].Rotation = -1.4f;
        Transform2D[] kicked = RigRig.WorldBones(package, pose);
        IReadOnlyList<Vector2> points = Hang(package, simulation, kicked, 0.3f);

        (Vector2 From, Vector2 To) thigh = RigClothLayout.Capsule(
            package,
            new RigClothCollider { BoneId = "thigh" },
            kicked)!.Value;
        Vector2 axis = (thigh.To - thigh.From).Normalized();
        float length = thigh.From.DistanceTo(thigh.To);
        int front = dress.Columns - 1;
        int above = 0;
        for (int row = 0; row < dress.Rows; row++)
        {
            Vector2 point = points[dress.Index(front, row)];
            float along = (point - thigh.From).Dot(axis);
            if (dress.IsPinned(front, row) || along < 0f || along > length)
                continue;

            Vector2 core = thigh.From + axis * along;
            Assert.That(point.Y, Is.LessThan(core.Y), $"front edge row {row} rides on top of the thigh");
            above++;
        }

        Assert.That(above, Is.GreaterThan(0), "the front edge reaches over the thigh");
    }

    [Test]
    public void DressesSurviveASaveACopyAndABoneRename()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        dress.Fill = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        dress.Lean = 40f;
        dress.ZOrder = 5;
        RigClothAuthoring.ToggleTether(package, 0, 0, dress.Rows - 1, 4);
        RigClothAuthoring.SetTetherRadius(package, 0, 30f);

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));
        Assert.That(RigPackageCopy.AreEqual(package, loaded), Is.True);
        Assert.That(RigPackageCopy.AreEqual(package, RigPackageCopy.Clone(package)), Is.True);

        Assert.That(loaded.Cloths[0].Tethers, Has.Count.EqualTo(1));
        Assert.That(loaded.Cloths[0].Tethers[0].Radius, Is.EqualTo(30f));

        RigRigAuthoring.RenameBone(package, Thigh, "leg");
        Assert.That(package.Cloths[0].Colliders.ConvertAll(collider => collider.BoneId), Does.Contain("leg"));
        RigRigAuthoring.RenameBone(package, 4, "calf");
        Assert.That(package.Cloths[0].Tethers[0].BoneId, Is.EqualTo("calf"));
        Assert.That(RigValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void ABoneADressUsesCannotBeDeleted()
    {
        RigPackage package = Dressed();
        Assert.That(
            package.Cloths[0].Colliders.Exists(collider => collider.BoneId == "foot"),
            Is.False,
            "the foot is below the hem, so the dress leaves it out");

        RigClothAuthoring.ToggleCollider(package, 0, 5);

        Assert.That(RigRigAuthoring.DeleteBone(package, 5), Is.False, "the foot is now a collider");
    }

    [Test]
    public void APointCyclesFromFreeToPinnedToTetheredToFree()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        int row = dress.Rows - 1;

        RigClothAuthoring.CyclePoint(package, 0, 0, row, 4);
        Assert.That((dress.IsPinned(0, row), dress.IsTethered(0, row)), Is.EqualTo((true, false)));
        RigClothAuthoring.CyclePoint(package, 0, 0, row, 4);
        Assert.That((dress.IsPinned(0, row), dress.IsTethered(0, row)), Is.EqualTo((false, true)));
        RigClothAuthoring.CyclePoint(package, 0, 0, row, 4);
        Assert.That((dress.IsPinned(0, row), dress.IsTethered(0, row)), Is.EqualTo((false, false)));
    }

    [Test]
    public void ATetheredHemStaysWithinReachOfItsBone()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        int hem = dress.Index(dress.Columns / 2, dress.Rows - 1);
        RigClothAuthoring.ToggleTether(package, 0, dress.Columns / 2, dress.Rows - 1, Torso);
        RigClothAuthoring.SetTetherRadius(package, 0, 10f);
        Transform2D[] rest = Rest(package);
        Vector2 onBone = rest[Torso].AffineInverse() * RigClothLayout.RestPoints(package, dress, rest)[hem];

        var simulation = new RigClothSimulation();
        Hang(package, simulation, rest);
        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[0].Translation = new Vector2(300, 0);
        Transform2D[] moved = RigRig.WorldBones(package, pose);
        simulation.Update(package, moved, RigClothSimulation.FixedStep * 2);

        Assert.That(
            simulation.Points(0)[hem].DistanceTo(moved[Torso] * onBone),
            Is.LessThanOrEqualTo(10.5f),
            "the body lurched 300 px, and the hem came with it");
    }

    [Test]
    public void TheSkirtNeverFoldsThroughItself()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        var simulation = new RigClothSimulation();
        Hang(package, simulation, Rest(package));
        RigPose pose = RigClothLayout.RestPose(package);
        pose.Bones[Torso].Rotation = 1.2f;
        IReadOnlyList<Vector2> points = Hang(package, simulation, RigRig.WorldBones(package, pose), 0.5f);

        int[] triangles = RigClothLayout.Triangles(dress.Columns, dress.Rows);
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector2 a = points[triangles[t]];
            float area = (points[triangles[t + 1]] - a).Cross(points[triangles[t + 2]] - a);
            Assert.That(area, Is.GreaterThan(0f), $"triangle {t / 3} kept its winding");
        }
    }

    [Test]
    public void BadClothsAreRefused()
    {
        RigPackage package = Dressed();
        package.Cloths[0].Rows = 1;
        package.Cloths[0].Pins.Add(new RigClothPin { Column = 99, Row = 0, BoneId = "nobody" });

        IReadOnlyList<string> issues = RigValidation.Validate(package);

        Assert.That(issues, Has.Some.Contains("invalid grid"));
        Assert.That(issues, Has.Some.Contains("outside its grid"));
        Assert.That(issues, Has.Some.Contains("missing bone 'nobody'"));
    }

    [Test]
    public void PinsToggleAndShrinkingTheGridDropsPinsThatNoLongerFit()
    {
        RigPackage package = Dressed();
        RigCloth dress = package.Cloths[0];
        int bottom = dress.Rows - 1;

        RigClothAuthoring.TogglePin(package, 0, 0, bottom, Thigh);
        Assert.That(dress.IsPinned(0, bottom), Is.True);

        RigClothAuthoring.SetGrid(package, 0, dress.Columns, bottom);
        Assert.That(dress.Pins.TrueForAll(pin => pin.Row < dress.Rows), Is.True);
    }
}
