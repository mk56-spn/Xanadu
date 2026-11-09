using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// Stored skins: the outline read from SVG path data, the mesh triangulated
/// inside it, weights worked out by heat diffusion and painted by hand, and
/// the whole saved with the rig.
/// </summary>
[TestFixture]
public sealed class XafSkinMeshTests
{
    private static float Area(Vector2[] points, int[] triangles)
    {
        float area = 0f;
        for (int t = 0; t < triangles.Length; t += 3)
            area += MathF.Abs((points[triangles[t + 1]] - points[triangles[t]]).Cross(points[triangles[t + 2]] - points[triangles[t]])) / 2f;
        return area;
    }

    [Test]
    public void PathDataReadsLinesCurvesAndRelativeMoves()
    {
        List<List<Vector2>> shapes = XafSvgOutline.Parse("M 0 0 L 10 0 l 0 10 H 0 Z m 20 0 C 25 -5, 30 5, 35 0 v 5 h -15 z");

        Assert.That(shapes, Has.Count.EqualTo(2));
        Assert.That(shapes[0], Is.EqualTo(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) }));
        Assert.That(shapes[1][0], Is.EqualTo(new Vector2(20, 0)), "a relative move after a close starts from the shape's start");
        Assert.That(shapes[1].Count, Is.GreaterThan(6), "the curve is flattened into many points");
        Assert.That(XafSvgOutline.Inside(new[] { shapes[0].ToArray() }, new Vector2(5, 5)), Is.True);
        Assert.That(XafSvgOutline.Inside(new[] { shapes[0].ToArray() }, new Vector2(15, 5)), Is.False);
    }

    [Test]
    public void AMeshFillsItsOutlineExactlyAndNoMore()
    {
        // An L: its inner corner must stay empty.
        Vector2[] l = { new(0, 0), new(10, 0), new(10, 30), new(30, 30), new(30, 40), new(0, 40) };
        (Vector2[] points, int[] triangles) = XafSkinBuilder.Build(new[] { l }, 2f);

        Assert.That(Area(points, triangles), Is.EqualTo(10 * 40 + 20 * 10).Within(25f));
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector2 middle = (points[triangles[t]] + points[triangles[t + 1]] + points[triangles[t + 2]]) / 3f;
            Assert.That(middle.X > 10.5f && middle.Y < 29.5f, Is.False, $"a triangle in the L's notch at {middle}");
        }
        var used = new HashSet<int>(triangles);
        Assert.That(used, Has.Count.EqualTo(points.Length), "every point is in a triangle");
    }

    /// <summary>A strip 10 wide and 100 long, down from the origin, its mesh, and two bones along it meeting half way.</summary>
    private static (Vector2[] Points, int[] Triangles, Vector2[] Outline) Strip()
    {
        Vector2[] outline = { new(-5, 0), new(5, 0), new(5, 100), new(-5, 100) };
        (Vector2[] points, int[] triangles) = XafSkinBuilder.Build(new[] { outline }, 2.5f);
        return (points, triangles, outline);
    }

    [Test]
    public void HeatGivesEachEndToItsBoneAndBlendsWhereTheyMeet()
    {
        var (points, triangles, outline) = Strip();
        var bones = new List<(Vector2, Vector2)> { (new Vector2(0, 0), new Vector2(0, 50)), (new Vector2(0, 50), new Vector2(0, 100)) };

        float[][] weights = XafHeatWeights.Compute(points, triangles, bones, spot => XafSvgOutline.Inside(new[] { outline }, spot));

        foreach (float[] row in weights)
            Assert.That(row.Sum(), Is.EqualTo(1f).Within(1e-4f));
        float At(Vector2 spot, int bone) =>
            weights[Enumerable.Range(0, points.Length).OrderBy(i => points[i].DistanceTo(spot)).First()][bone];
        Assert.That(At(new Vector2(0, 10), 0), Is.GreaterThan(0.95f));
        Assert.That(At(new Vector2(0, 90), 1), Is.GreaterThan(0.95f));
        Assert.That(At(new Vector2(0, 50), 0), Is.InRange(0.2f, 0.8f), "the joint is shared");
    }

    [Test]
    public void HeatDoesNotCrossAGapInTheArt()
    {
        Vector2[] left = { new(-15, 0), new(-5, 0), new(-5, 100), new(-15, 100) };
        Vector2[] right = { new(5, 0), new(15, 0), new(15, 100), new(5, 100) };
        var outline = new[] { left, right };
        (Vector2[] points, int[] triangles) = XafSkinBuilder.Build(outline, 2.5f);
        var bones = new List<(Vector2, Vector2)> { (new Vector2(-10, 0), new Vector2(-10, 100)), (new Vector2(10, 0), new Vector2(10, 100)) };

        float[][] weights = XafHeatWeights.Compute(points, triangles, bones, spot => XafSvgOutline.Inside(outline, spot));

        for (int i = 0; i < points.Length; i++)
            Assert.That(weights[i][points[i].X < 0 ? 0 : 1], Is.EqualTo(1f).Within(1e-4f), $"point {points[i]}");
    }

    [Test]
    public void PaintingRaisesOneBoneAndSharesTheRestAsBefore()
    {
        var skin = new XafSkinMesh
        {
            Points = new[] { Vector2.Zero, Vector2.One },
            Bones = { "a", "b", "c" },
            Weights = new[] { new[] { 0.5f, 0.3f, 0.2f }, new[] { 1f, 0f, 0f } },
        };

        XafSkinAuthoring.Paint(skin, "c", new[] { (0, 1f) }, 0.4f);

        Assert.That(skin.Weights[0][2], Is.EqualTo(0.6f).Within(1e-4f));
        Assert.That(skin.Weights[0][0] / skin.Weights[0][1], Is.EqualTo(0.5f / 0.3f).Within(1e-3f), "the others keep their proportions");
        Assert.That(skin.Weights[0].Sum(), Is.EqualTo(1f).Within(1e-4f));

        XafSkinAuthoring.Paint(skin, "d", new[] { (1, 0.5f) }, 0.4f);
        Assert.That(skin.Bones, Is.EqualTo(new[] { "a", "b", "c", "d" }), "a new bone joins the skin");
        Assert.That(skin.Weights[1][3], Is.EqualTo(0.2f).Within(1e-4f));

        XafSkinAuthoring.Paint(skin, "a", new[] { (1, 1f) }, -1f);
        Assert.That(skin.Weights[1][0], Is.Zero);
        Assert.That(skin.Weights[1].Sum(), Is.EqualTo(1f).Within(1e-4f));
    }

    private static XafPackage SkinnedStrip()
    {
        XafPackage package = XafRigAuthoring.CreateEmptyPackage();
        int upper = XafRigAuthoring.AddBone(package, 0, "upper", Vector2.Zero);
        int lower = XafRigAuthoring.AddBone(package, upper, "lower", new Vector2(0, 50));
        package.Skeleton.Bones[lower].Tip = new Vector2(0, 50);
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://strip.svg", Group = "strip", BoneId = "upper",
            Transform = new Transform2D(0f, new Vector2(0, 50)),
        });
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 100"><g id="strip"><path d="M 0 0 H 10 V 100 H 0 Z"/></g></svg>
            """;
        Assert.That(XafSkinAuthoring.Build(package, 0, svg, 2.5f), Is.True);
        return package;
    }

    [Test]
    public void AnAttachmentIsTracedAndWeightedToTheBonesThatBendIt()
    {
        XafPackage package = SkinnedStrip();
        XafSkinMesh skin = package.Attachments[0].Mesh!.Skin!;

        Assert.That(skin.Bones, Is.EqualTo(new[] { "upper", "lower" }));
        Assert.That(skin.ArtSize, Is.EqualTo(new Vector2(10, 100)));
        int bottom = Enumerable.Range(0, skin.Points.Length).OrderBy(i => skin.Points[i].DistanceTo(new Vector2(5, 100))).First();
        Assert.That(skin.Weights[bottom][1], Is.GreaterThan(0.95f), "the far end follows the lower bone");

        // Bending the lower bone carries the far end with it; the near end stays.
        XafPose pose = XafClothLayout.RestPose(package);
        pose.Bones[2].Rotation = Mathf.Pi / 2f;
        Vector2[] posed = XafSkinAuthoring.Posed(package, 0, pose);
        Vector2[] rest = XafSkinAuthoring.Posed(package, 0, XafClothLayout.RestPose(package));
        int top = Enumerable.Range(0, skin.Points.Length).OrderBy(i => skin.Points[i].DistanceTo(new Vector2(5, 0))).First();
        Assert.That(posed[top].DistanceTo(rest[top]), Is.LessThan(1e-3f));
        Assert.That(posed[bottom].DistanceTo(new Vector2(-50, 50)), Is.LessThan(2f));
    }

    [Test]
    public void SkinsAreSaved()
    {
        XafPackage package = SkinnedStrip();

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));
        Assert.That(XafSkinMesh.AreEqual(loaded.Attachments[0].Mesh!.Skin, package.Attachments[0].Mesh!.Skin), Is.True);
        Assert.That(XafPackageCopy.AreEqual(package, XafPackageCopy.Clone(package)), Is.True);
    }

    [Test]
    public void TheContactRigsRightLegIsASkinWeightedHipToToe()
    {
        string file = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Tools/Animator/Animations/Characters/contact.xaf");
        XafPackage package = XafBinary.Load(Path.GetFullPath(file));
        XafSkinMesh skin = package.Attachments.Single(attachment => attachment.Group == "leg_r").Mesh!.Skin!;

        Assert.That(skin.Points.Length, Is.GreaterThan(100));
        Assert.That(skin.Bones, Is.EqualTo(new[] { "thigh_r", "shin_r", "foot_r", "torso" }));
        float[] Near(Vector2 spot) => skin.Weights[Enumerable.Range(0, skin.Points.Length).OrderBy(i => skin.Points[i].DistanceTo(spot)).First()];
        Assert.That(Near(new Vector2(15, 30))[0], Is.GreaterThan(0.9f), "mid thigh");
        Assert.That(Near(new Vector2(12, 75))[1], Is.GreaterThan(0.9f), "mid shin");
        Assert.That(Near(new Vector2(37, 109))[2], Is.GreaterThan(0.9f), "toes");
    }
}
