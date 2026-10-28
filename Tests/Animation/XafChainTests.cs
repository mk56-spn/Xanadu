using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafChainTests
{
    private static XafPackage Package()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "head", new Vector2(0, -80));
        return package;
    }

    private static XafChain Hair(string bone = "head") => new()
    {
        Id = "hair",
        BoneId = bone,
        Offset = new Vector2(3, -4),
        Segments = 5,
        SegmentLength = 12f,
        Gravity = 700f,
        Damping = 0.9f,
        SvgPath = "res://Assets/SVG/Char/hair_segment.svg",
        Group = "hair_segment",
        Width = 7f,
        Lean = 120f,
        CollideBoneId = "head",
        CollideOffset = new Vector2(1, 2),
        CollideRadius = 9f,
    };

    [Test]
    public void ChainsSurviveASaveAndLoad()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(loaded.Chains, Has.Count.EqualTo(1));
        Assert.That(XafPackageCopy.AreEqual(package, loaded), Is.True);
        XafChain chain = loaded.Chains[0];
        Assert.That(chain.Id, Is.EqualTo("hair"));
        Assert.That(chain.BoneId, Is.EqualTo("head"));
        Assert.That(chain.Offset, Is.EqualTo(new Vector2(3, -4)));
        Assert.That(chain.Segments, Is.EqualTo(5));
        Assert.That(chain.SegmentLength, Is.EqualTo(12f));
        Assert.That(chain.Gravity, Is.EqualTo(700f));
        Assert.That(chain.Damping, Is.EqualTo(0.9f));
        Assert.That(chain.SvgPath, Is.EqualTo("res://Assets/SVG/Char/hair_segment.svg"));
        Assert.That(chain.Group, Is.EqualTo("hair_segment"));
        Assert.That(chain.Width, Is.EqualTo(7f));
        Assert.That(chain.Lean, Is.EqualTo(120f));
        Assert.That(chain.CollideBoneId, Is.EqualTo("head"));
        Assert.That(chain.CollideOffset, Is.EqualTo(new Vector2(1, 2)));
        Assert.That(chain.CollideRadius, Is.EqualTo(9f));
    }

    [Test]
    public void ChainsFromBeforeArtAndColliderLoadWithTheDefaultArtAndNoCollider()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package, minor: 2));

        XafChain chain = loaded.Chains[0];
        Assert.That(chain.Segments, Is.EqualTo(5));
        Assert.That(chain.Gravity, Is.EqualTo(700f));
        Assert.That(chain.SvgPath, Is.EqualTo(XafChain.DefaultSvgPath));
        Assert.That(chain.Group, Is.EqualTo(XafChain.DefaultGroup));
        Assert.That(chain.HasCollider, Is.False);
        Assert.That(chain.Lean, Is.EqualTo(0f));
    }

    [Test]
    public void ValidationRejectsHalfAnArtReferenceAndABadColliderOrWidth()
    {
        XafPackage package = Package();
        package.Chains.Add(new XafChain
        {
            Id = "a", BoneId = "head", SvgPath = "res://x.svg", Group = "",
        });
        package.Chains.Add(new XafChain { Id = "b", BoneId = "head", Width = 0f });
        package.Chains.Add(new XafChain
        {
            Id = "c", BoneId = "head", CollideBoneId = "nowhere", CollideRadius = 5f,
        });
        package.Chains.Add(new XafChain { Id = "d", BoneId = "head", CollideRadius = -1f });
        package.Chains.Add(new XafChain { Id = "e", BoneId = "head", SvgPath = "", Group = "" });

        string message = string.Join(" ", XafValidation.Validate(package));

        Assert.That(message, Does.Contain("Chain 'a' needs both an SVG path and a group"));
        Assert.That(message, Does.Contain("Chain 'b' has an invalid width"));
        Assert.That(message, Does.Contain("collides with missing bone 'nowhere'"));
        Assert.That(message, Does.Contain("Chain 'd' has an invalid collider"));
        Assert.That(message, Does.Not.Contain("Chain 'e'"));
    }

    [Test]
    public void ACopyKeepsTheArtAndColliderToo()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());

        XafPackage copy = XafPackageCopy.Clone(package);
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.True);

        copy.Chains[0].CollideRadius = 1f;
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.False);
        copy.Chains[0].CollideRadius = 9f;
        copy.Chains[0].Lean = 0f;
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.False);
    }

    [Test]
    public void ANewChainStartsAlongTheWayItIsPulledAndNeverInsideItsCollider()
    {
        XafPackage package = Package();
        XafChain chain = Hair();
        chain.Gravity = 900f;
        chain.Lean = 150f;
        chain.Offset = Vector2.Zero;
        chain.CollideBoneId = "head";
        chain.CollideOffset = new Vector2(12, 30);
        chain.CollideRadius = 9f;
        package.Chains.Add(chain);
        Transform2D[] world = { Transform2D.Identity, Transform2D.Identity };
        var simulation = new XafChainSimulation();

        simulation.Update(package, world, 1f / 60f);

        var points = simulation.Points(0);
        Vector2 along = (points[^1] - points[0]).Normalized();
        Assert.That(along.AngleTo(new Vector2(150, 900).Normalized()), Is.EqualTo(0f).Within(0.05f));
        foreach (Vector2 point in points)
            Assert.That(point.DistanceTo(new Vector2(12, 30)), Is.GreaterThanOrEqualTo(9f - 0.001f));
    }

    [Test]
    public void LeanPullsAHangingChainOffTheVertical()
    {
        XafPackage package = Package();
        XafChain chain = Hair();
        chain.CollideRadius = 0f;
        chain.Lean = 300f;
        chain.Offset = Vector2.Zero;
        package.Chains.Add(chain);
        Transform2D[] world = { Transform2D.Identity, Transform2D.Identity };
        var simulation = new XafChainSimulation();

        for (int i = 0; i < 600; i++)
            simulation.Update(package, world, 1f / 60f);

        var points = simulation.Points(0);
        Assert.That(points[^1].X, Is.GreaterThan(points[0].X + 10f));
        Assert.That(points[^1].Y, Is.GreaterThan(points[0].Y + 20f));
    }

    [Test]
    public void FilesFromBeforeChainsStillLoadWithNone()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package, minor: 1));

        Assert.That(loaded.Minor, Is.EqualTo(1));
        Assert.That(loaded.Chains, Is.Empty);
        Assert.That(loaded.Skeleton.Bones, Has.Count.EqualTo(2));
    }

    [Test]
    public void ACopyHoldsItsOwnChains()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());

        XafPackage copy = XafPackageCopy.Clone(package);
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.True);

        copy.Chains[0].Gravity = 1f;
        Assert.That(package.Chains[0].Gravity, Is.EqualTo(700f));
        Assert.That(XafPackageCopy.AreEqual(package, copy), Is.False);
    }

    [Test]
    public void ValidationRejectsBadChains()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair("missing"));
        package.Chains.Add(Hair());
        package.Chains.Add(new XafChain { Id = "bad", BoneId = "head", Segments = 0, SegmentLength = 0f });

        string message = string.Join(" ", XafValidation.Validate(package));

        Assert.That(message, Does.Contain("missing bone 'missing'"));
        Assert.That(message, Does.Contain("Duplicate chain id 'hair'"));
        Assert.That(message, Does.Contain("Chain 'bad' has an invalid segment count"));
        Assert.That(message, Does.Contain("Chain 'bad' has an invalid segment length"));
    }

    [Test]
    public void AddingNamesTheChainAfterItsBoneAndNumbersRepeats()
    {
        XafPackage package = Package();

        int first = XafChainAuthoring.Add(package, 1);
        int second = XafChainAuthoring.Add(package, 1);

        Assert.That(package.Chains[first].Id, Is.EqualTo("head_chain"));
        Assert.That(package.Chains[second].Id, Is.EqualTo("head_chain2"));
        Assert.That(package.Chains[first].BoneId, Is.EqualTo("head"));
        Assert.That(XafValidation.Validate(package), Is.Empty);
        Assert.That(XafChainAuthoring.Add(package, 9), Is.EqualTo(-1));
    }

    [Test]
    public void SettingsAreClampedToWhatAChainAllows()
    {
        XafPackage package = Package();
        XafChainAuthoring.Add(package, 1);

        XafChainAuthoring.SetSegments(package, 0, 1000);
        XafChainAuthoring.SetSegmentLength(package, 0, -3f);
        XafChainAuthoring.SetDamping(package, 0, 7f);

        XafChain chain = package.Chains[0];
        Assert.That(chain.Segments, Is.EqualTo(XafChain.MaxSegments));
        Assert.That(chain.SegmentLength, Is.EqualTo(XafChain.MinSegmentLength));
        Assert.That(chain.Damping, Is.EqualTo(1f));
        Assert.That(XafChainAuthoring.SetGravity(package, 4, 1f), Is.False);
    }

    [Test]
    public void RenamingABoneMovesItsChains()
    {
        XafPackage package = Package();
        XafChainAuthoring.Add(package, 1);

        Assert.That(XafRigAuthoring.RenameBone(package, 1, "skull"), Is.True);

        Assert.That(package.Chains[0].BoneId, Is.EqualTo("skull"));
        Assert.That(XafValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void ABoneWithAChainCannotBeDeleted()
    {
        XafPackage package = Package();
        XafChainAuthoring.Add(package, 1);

        Assert.That(XafRigAuthoring.DeleteBone(package, 1), Is.False);

        XafChainAuthoring.Delete(package, 0);
        Assert.That(XafRigAuthoring.DeleteBone(package, 1), Is.True);
    }

    [Test]
    public void TheChainHangsFromItsBoneAndKeepsItsLinkLengths()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var simulation = new XafChainSimulation();
        Transform2D[] world = { Transform2D.Identity, new(0f, new Vector2(100, 50)) };

        for (int i = 0; i < 120; i++)
            simulation.Update(package, world, 1f / 60f);

        var points = simulation.Points(0);
        Assert.That(points, Has.Count.EqualTo(6));
        Assert.That(points[0].DistanceTo(new Vector2(103, 46)), Is.LessThan(0.001f));
        for (int i = 1; i < points.Count; i++)
            Assert.That(points[i].DistanceTo(points[i - 1]), Is.EqualTo(12f).Within(0.1f));
        Assert.That(points[^1].Y, Is.GreaterThan(points[0].Y + 40f));
    }

    [Test]
    public void MovingTheBoneDragsTheChainAlong()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var simulation = new XafChainSimulation();
        Transform2D[] world = { Transform2D.Identity, new(0f, new Vector2(0, 0)) };
        for (int i = 0; i < 60; i++)
            simulation.Update(package, world, 1f / 60f);
        float settled = simulation.Points(0)[^1].X;

        world[1] = new Transform2D(0f, new Vector2(200, 0));
        for (int i = 0; i < 60; i++)
            simulation.Update(package, world, 1f / 60f);

        Assert.That(simulation.Points(0)[^1].X, Is.GreaterThan(settled + 150f));
    }

    [Test]
    public void ChangingSettingsActsLiveWithoutRebuildingTheChain()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var simulation = new XafChainSimulation();
        Transform2D[] world = { Transform2D.Identity, Transform2D.Identity };
        for (int i = 0; i < 120; i++)
            simulation.Update(package, world, 1f / 60f);
        Vector2 hanging = simulation.Points(0)[^1];

        package.Chains[0].Gravity = 0f;
        simulation.Update(package, world, 1f / 60f);
        Assert.That(simulation.Points(0)[^1].DistanceTo(hanging), Is.LessThan(1f));

        package.Chains[0].Segments = 3;
        simulation.Update(package, world, 1f / 60f);
        Assert.That(simulation.Points(0), Has.Count.EqualTo(4));
    }

    [Test]
    public void ALongFrameCatchesUpInFixedSteps()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var steady = new XafChainSimulation();
        var extended = new XafChainSimulation();
        Transform2D[] start = { Transform2D.Identity, Transform2D.Identity };
        steady.Update(package, start, 1f / 60f);
        extended.Update(package, start, 1f / 60f);

        // Swing both by moving the bone: ten 60 Hz frames against one frame ten steps long.
        Transform2D[] moved = { Transform2D.Identity, new(0f, new Vector2(100, 0)) };
        for (int i = 0; i < 10; i++)
            steady.Update(package, moved, 1f / 60f);
        extended.Update(package, moved, 10f / 60f);

        Assert.That(steady.Points(0)[^1].X, Is.GreaterThan(5f));
        Assert.That(extended.Points(0)[^1].DistanceTo(steady.Points(0)[^1]), Is.LessThan(0.01f));
    }

    [Test]
    public void AStalledFrameIsCappedInsteadOfSimulatingTheWholeStall()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var capped = new XafChainSimulation();
        var quarter = new XafChainSimulation();
        Transform2D[] start = { Transform2D.Identity, Transform2D.Identity };
        capped.Update(package, start, 1f / 60f);
        quarter.Update(package, start, 1f / 60f);

        Transform2D[] moved = { Transform2D.Identity, new(0f, new Vector2(100, 0)) };
        capped.Update(package, moved, 30f);
        quarter.Update(package, moved, 0.25f);

        Assert.That(capped.Points(0)[^1].DistanceTo(quarter.Points(0)[^1]), Is.LessThan(0.01f));
    }

    [Test]
    public void ResetDropsTheMotion()
    {
        XafPackage package = Package();
        package.Chains.Add(Hair());
        var simulation = new XafChainSimulation();
        Transform2D[] world = { Transform2D.Identity, Transform2D.Identity };
        for (int i = 0; i < 60; i++)
            simulation.Update(package, world, 1f / 60f);

        simulation.Reset();

        Assert.That(simulation.Points(0), Is.Empty);
    }

    private static XafPackage TwoBones()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        XafRigAuthoring.AddBone(package, 0, "torso", new Vector2(10, -80));
        XafRigAuthoring.AddBone(package, 1, "head", new Vector2(5, -70));
        return package;
    }

    private static Vector2 RootInRestWorld(XafPackage package, XafChain chain) =>
        XafRig.RestWorld(package)[XafRig.FindBone(package, chain.BoneId)] * chain.Offset;

    [Test]
    public void TheRestWorldPutsEachBoneWhereItsOffsetsAddUp()
    {
        Transform2D[] world = XafRig.RestWorld(TwoBones());

        Assert.That(world[0].Origin, Is.EqualTo(Vector2.Zero));
        Assert.That(world[1].Origin, Is.EqualTo(new Vector2(10, -80)));
        Assert.That(world[2].Origin, Is.EqualTo(new Vector2(15, -150)));
    }

    [Test]
    public void ArtCanBeChosenAndClearedAndAHalfReferenceClearsBoth()
    {
        XafPackage package = TwoBones();
        XafChainAuthoring.Add(package, 2);

        Assert.That(XafChainAuthoring.SetArt(package, 0, "res://Assets/SVG/leaf.svg", "leaf"), Is.True);
        Assert.That(package.Chains[0].SvgPath, Is.EqualTo("res://Assets/SVG/leaf.svg"));
        Assert.That(package.Chains[0].Group, Is.EqualTo("leaf"));
        Assert.That(package.Chains[0].HasArt, Is.True);

        XafChainAuthoring.SetArt(package, 0, "res://Assets/SVG/leaf.svg", "");
        Assert.That(package.Chains[0].HasArt, Is.False);
        Assert.That(package.Chains[0].SvgPath, Is.EqualTo(""));
        Assert.That(XafValidation.Validate(package), Is.Empty);
        Assert.That(XafChainAuthoring.SetArt(package, 5, "a", "b"), Is.False);
    }

    [Test]
    public void RehangingAChainOnAnotherBoneKeepsItsFirstPointWhereItWas()
    {
        XafPackage package = TwoBones();
        XafChainAuthoring.Add(package, 2);
        XafChainAuthoring.SetOffset(package, 0, new Vector2(-4, -17));
        Vector2 before = RootInRestWorld(package, package.Chains[0]);

        Assert.That(XafChainAuthoring.SetBone(package, 0, 1), Is.True);

        Assert.That(package.Chains[0].BoneId, Is.EqualTo("torso"));
        Assert.That(RootInRestWorld(package, package.Chains[0]).DistanceTo(before), Is.LessThan(0.001f));
        Assert.That(package.Chains[0].Offset, Is.Not.EqualTo(new Vector2(-4, -17)));
        Assert.That(XafValidation.Validate(package), Is.Empty);
        Assert.That(XafChainAuthoring.SetBone(package, 0, 9), Is.False);
        Assert.That(XafChainAuthoring.SetBone(package, 3, 1), Is.False);
    }

    [Test]
    public void PuttingAColliderOnABoneCreatesOneCentredThereThenMovesItWithoutMovingIt()
    {
        XafPackage package = TwoBones();
        XafChainAuthoring.Add(package, 2);
        XafChain chain = package.Chains[0];
        Assert.That(chain.HasCollider, Is.False);

        Assert.That(XafChainAuthoring.SetColliderBone(package, 0, 1), Is.True);
        Assert.That(chain.CollideBoneId, Is.EqualTo("torso"));
        Assert.That(chain.CollideRadius, Is.EqualTo(XafChainAuthoring.DefaultColliderRadius));
        Assert.That(chain.CollideOffset, Is.EqualTo(Vector2.Zero));

        XafChainAuthoring.SetCollideOffset(package, 0, new Vector2(7, 3));
        Transform2D[] world = XafRig.RestWorld(package);
        Vector2 before = world[1] * chain.CollideOffset;

        XafChainAuthoring.SetColliderBone(package, 0, 2);

        Assert.That(chain.CollideBoneId, Is.EqualTo("head"));
        Assert.That((world[2] * chain.CollideOffset).DistanceTo(before), Is.LessThan(0.001f));
        Assert.That(chain.CollideRadius, Is.EqualTo(XafChainAuthoring.DefaultColliderRadius), "an existing size is kept");
        Assert.That(XafValidation.Validate(package), Is.Empty);
    }

    [Test]
    public void AnExistingColliderSizeSurvivesMovingItToAnotherBone()
    {
        XafPackage package = TwoBones();
        XafChainAuthoring.Add(package, 2);
        XafChainAuthoring.SetColliderBone(package, 0, 1);
        XafChainAuthoring.SetCollideRadius(package, 0, 14f);

        XafChainAuthoring.SetColliderBone(package, 0, 2);

        Assert.That(package.Chains[0].CollideRadius, Is.EqualTo(14f));
    }
}
