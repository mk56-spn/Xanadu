using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

/// <summary>
/// The shipped contact rig: its bones, its cut-up SVG art and the clips that
/// drive it have to keep agreeing with each other as either is edited.
/// </summary>
[TestFixture]
public sealed class ContactRigAssetTests
{
    private const string RigFile = "Tools/Animator/Animations/Characters/contact.xaf";

    private static string ProjectFile(string relative)
    {
        for (string? dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(relative);
    }

    private static XafPackage Load() => XafBinary.Load(ProjectFile(RigFile));

    /// <summary>The part's own rectangle (its viewBox), which is what the attachment is centred on.</summary>
    private static Rect2 ViewBox(XafAttachment attachment, out string svg)
    {
        svg = File.ReadAllText(ProjectFile("Tools/Animator/" + attachment.SvgPath["res://".Length..]));
        int start = svg.IndexOf("viewBox=\"", StringComparison.Ordinal) + "viewBox=\"".Length;
        float[] v = Array.ConvertAll(
            svg[start..svg.IndexOf('"', start)].Split(' '),
            number => float.Parse(number, CultureInfo.InvariantCulture));
        return new Rect2(v[0], v[1], v[2], v[3]);
    }

    private static XafPose RestPoseOf(XafPackage package)
    {
        var pose = new XafPose(package.Skeleton.Bones.Count, package.Attachments.Count);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        for (int i = 0; i < pose.Attachments.Length; i++)
            pose.Attachments[i] = XafTransform.From(package.Attachments[i].Transform);
        return pose;
    }

    [Test]
    public void TheRigIsValidAndKeepsItsClips()
    {
        XafPackage package = Load();

        Assert.That(XafValidation.Validate(package), Is.Empty);
        Assert.That(package.Skeleton.Bones, Has.Count.EqualTo(15));
        // The animator can add clips of its own, such as a work-in-progress duplicate,
        // so this pins only the ones the game plays by name.
        Assert.That(package.Clips.ConvertAll(clip => clip.Id),
            Is.SupersetOf(new[]
            {
                "idle", "jumping", "landing", "death", "attack_low", "attack_mid",
                "attack_high", "attack_air_low", "attack_air_mid", "attack_air_high",
            }));
    }

    /// <summary>
    /// Once the rig has transition rules, the game's own hand-overs must be
    /// among them, or a jump or landing would leave the old clip playing.
    /// </summary>
    [Test]
    public void TheRigAllowsEveryHandOverTheGameAsksFor()
    {
        XafPackage package = Load();

        foreach ((string from, string to) in Xanadu.Systems.Characters.CharacterMotionSystem.Transitions(package))
            Assert.That(XafTransitions.Allows(package, from, to), Is.True, $"{from} to {to}");
    }

    /// <summary>
    /// A part's layer is added to the animator's art canvas, which sits just above
    /// the preview panel, so a layer below -1 draws behind the panel and vanishes.
    /// The parts therefore share one layer and overlap in list order, which is
    /// the source drawing's paint order: far limbs, then torso, then near limbs.
    /// </summary>
    [Test]
    public void ThePartsOverlapInListOrderOnALayerThatIsNotHiddenBehindThePanels()
    {
        XafPackage package = Load();
        int Index(string bone, string group) =>
            package.Attachments.FindIndex(a => a.BoneId == bone && a.Group == group);

        Assert.That(package.Attachments.TrueForAll(a => a.ZOrder >= -1), Is.True);
        Assert.That(Index("thigh_r", "thigh_r"), Is.LessThan(Index("torso", "torso")));
        Assert.That(Index("torso", "torso"), Is.LessThan(Index("thigh_l", "thigh_l")));
        Assert.That(Index("torso", "neck"), Is.LessThan(Index("head", "head")));
        Assert.That(Index("forearm_r", "forearm_r"), Is.LessThan(Index("torso", "torso")));
        Assert.That(Index("torso", "torso"), Is.LessThan(Index("forearm_l", "forearm_l")));
        Assert.That(Index("forearm_l", "forearm_l"), Is.LessThan(Index("hand_l", "hand")));
    }

    [Test]
    public void EveryAttachmentNamesAGroupThatExistsInItsSvg()
    {
        foreach (XafAttachment attachment in Load().Attachments)
        {
            ViewBox(attachment, out string svg);
            Assert.That(XafSvgDocument.Groups(svg), Does.Contain(attachment.Group), attachment.SvgPath);
        }
    }

    private static XafChain Hair(XafPackage package) => package.Chains.Single(chain => chain.Id == "hair");

    [Test]
    public void TheHairHangsFromTheHeadAsGoldenRhombiAndRestsOnTheNearShoulder()
    {
        XafPackage package = Load();
        XafChain hair = Hair(package);

        Assert.That(hair.BoneId, Is.EqualTo("head"));
        Assert.That(hair.HasArt, Is.True);
        Assert.That(hair.CollideBoneId, Is.EqualTo("upper_arm_l"));
        Assert.That(hair.CollideRadius, Is.GreaterThan(0f));
        Assert.That(hair.Lean, Is.GreaterThan(0f), "leans the way the character faces, so it falls over the front");

        string svg = File.ReadAllText(ProjectFile("Tools/Animator/" + hair.SvgPath["res://".Length..]));
        Assert.That(XafSvgDocument.Groups(svg), Does.Contain(hair.Group));
        // One flat, light gold fill on a single rhombus: no outline and no second tone.
        Assert.That(svg, Does.Contain("fill=\"#f5d76e\""));
        Assert.That(svg, Does.Not.Contain("stroke"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(svg, "<path").Count, Is.EqualTo(1));
        Assert.That(XafChainArt.Layer(package), Is.GreaterThan(package.Attachments.Max(a => a.ZOrder)));
    }

    /// <summary>
    /// The hair settled at the rest pose, then carried through the clips the way the
    /// game plays them: one into the next with a short crossfade, at 60 Hz.
    /// </summary>
    private static IEnumerable<(XafChainSimulation Simulation, Transform2D[] World, string Clip)> HairThroughEveryClip(
        XafPackage package)
    {
        var runtime = new AnimatorRuntime(package);
        runtime.Play("idle", 0f);
        var simulation = new XafChainSimulation();
        Transform2D[] rest = XafRig.WorldBones(package, runtime.Pose);
        for (int i = 0; i < 180; i++)
            simulation.Update(package, rest, 1f / 60f);

        foreach ((string clip, float seconds) in new[]
                 {
                     ("idle", 1.5f), ("jumping", 1.2f), ("landing", 0.3f), ("idle", 1f), ("jumping", 0.6f), ("death", 2f),
                 })
        {
            runtime.Play(clip, 0.1f);
            for (int frame = 0; frame < (int)(seconds * 60); frame++)
            {
                runtime.Update(1f / 60f);
                Transform2D[] world = XafRig.WorldBones(package, runtime.Pose);
                simulation.Update(package, world, 1f / 60f);
                yield return (simulation, world, clip);
            }
        }
    }

    [Test]
    public void TheHairNeverEntersTheShoulderOrStretchesInAnyFrameOfAnyClip()
    {
        XafPackage package = Load();
        XafChain hair = Hair(package);

        int frames = 0;
        foreach ((XafChainSimulation simulation, Transform2D[] world, string clip) in HairThroughEveryClip(package))
        {
            frames++;
            Vector2 shoulder = XafChainSimulation.Collider(package, hair, world)!.Value;
            var points = simulation.Points(0);
            for (int i = 1; i < points.Count; i++)
            {
                // These are the drawn points, blended between steps and carried with the head, so
                // they may dip a fraction of a pixel (0.4 at worst) into a collider the physics keeps out.
                Assert.That(points[i].DistanceTo(shoulder), Is.GreaterThanOrEqualTo(hair.CollideRadius - 0.6f), $"{clip} point {i}");
                // The worst a link stretches is the first one as the death clip starts, about 0.9.
                Assert.That(points[i].DistanceTo(points[i - 1]), Is.EqualTo(hair.SegmentLength).Within(1f), $"{clip} link {i}");
            }
        }

        Assert.That(frames, Is.GreaterThan(200));
    }

    [Test]
    public void AtRestTheHairHangsInFrontOfTheShoulderNotBehindIt()
    {
        XafPackage package = Load();
        XafChain hair = Hair(package);
        Transform2D[] world = XafRig.WorldBones(package, RestPoseOf(package));
        var simulation = new XafChainSimulation();
        for (int i = 0; i < 600; i++)
            simulation.Update(package, world, 1f / 60f);

        Vector2 shoulder = XafChainSimulation.Collider(package, hair, world)!.Value;
        var points = simulation.Points(0);
        Assert.That(points[0].DistanceTo(XafChainSimulation.Anchor(package, hair, world)!.Value), Is.LessThan(0.001f));
        foreach (Vector2 point in points)
        {
            if (point.Y >= shoulder.Y)
                Assert.That(point.X, Is.GreaterThan(shoulder.X + hair.CollideRadius / 2f), $"{point}");
        }

        // It hangs from the back of the head, above the shoulder, and reaches the chest.
        Assert.That(points[0].Y, Is.LessThan(shoulder.Y - hair.CollideRadius));
        Assert.That(points[^1].Y, Is.GreaterThan(shoulder.Y + 30f));
    }
}
