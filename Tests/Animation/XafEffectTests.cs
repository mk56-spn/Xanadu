using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

/// <summary>Effects: Xif items on bones that clips show, hide, fade and move.</summary>
[TestFixture]
public sealed class RigEffectTests
{
    [Test]
    public void AnEffectAndItsPolygonRoundTripThroughTheBinaryFormat()
    {
        RigPackage package = Package();
        RigEffect effect = package.Effects[0];
        effect.Transform = new Transform2D(0.5f, new Vector2(10, -4));
        effect.ZOrder = 7;
        effect.Item.Primitives[0].Ref = "streak.png";
        effect.Item.Shaders.Add(XifShaderRef.OfCommon("Assets/Shaders/Items/rainbow.slang"));

        RigPackage loaded = RigBinary.Read(RigBinary.Write(package));

        Assert.That(RigPackageCopy.AreEqual(package, loaded), Is.True);
        XifPrimitive polygon = loaded.Effects[0].Item.Primitives[0];
        Assert.That(polygon.Kind, Is.EqualTo(XifPrimitive.PolygonKind));
        Assert.That(polygon.Points, Is.EqualTo(RigEffects.StreakOutline()));
        Assert.That(polygon.Fill, Is.EqualTo(new Color(1f, 1f, 1f, 0.85f)));
        Assert.That(loaded.Effects[0].ZOrder, Is.EqualTo(7));
    }

    [Test]
    public void AnEffectIsHiddenUntilAClipShowsItAndHeldBetweenKeys()
    {
        RigPackage package = Package();

        Assert.That(Sample(package, 0.05f).Visible, Is.False);
        Assert.That(Sample(package, 0.1f).Visible, Is.True, "shown from its key exactly");
        Assert.That(Sample(package, 0.24f).Visible, Is.True, "held, not blended towards the hide key");
        Assert.That(Sample(package, 0.25f).Visible, Is.False);
        Assert.That(Sample(package, 0.1f).Alpha, Is.EqualTo(1f));
    }

    [Test]
    public void OpacityAndTransformKeysMoveTheEffect()
    {
        RigPackage package = Package();
        RigClip clip = package.Clips[0];
        RigAuthoring.KeyEffect(
            package,
            clip,
            0,
            0.1f,
            new RigEffectState
            {
                Transform = new RigTransform { Translation = new Vector2(0, 0), Scale = Vector2.One },
                Opacity = 1f
            });
        RigAuthoring.KeyEffect(
            package,
            clip,
            0,
            0.2f,
            new RigEffectState
            {
                Transform = new RigTransform { Translation = new Vector2(40, 0), Scale = Vector2.One },
                Opacity = 0f
            });

        RigEffectState middle = Sample(package, 0.15f);

        Assert.That(middle.Opacity, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(middle.Alpha, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(middle.Transform.Translation.X, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void ACrossfadeFadesAnEffectOutWithTheClipThatShowedIt()
    {
        RigPackage package = Package();
        RigPose shown = RigSampler.Sample(package, package.Clips[0], 0.15f);
        RigPose hidden = RigSampler.Sample(package, package.Clips[1], 0f);

        RigEffectState blended = RigBlend.Crossfade(shown, hidden, 0.25f).Effects[0];

        Assert.That(blended.Alpha, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(
            blended.Transform.Translation,
            Is.EqualTo(shown.Effects[0].Transform.Translation),
            "it stays where the clip showing it put it");
    }

    [Test]
    public void AnAdditiveLayerDrawsItsEffectOverTheBaseAtItsWeight()
    {
        RigPackage package = Package();
        RigPose base_ = RigSampler.Sample(package, package.Clips[1], 0f);
        RigPose layer = RigSampler.Sample(package, package.Clips[0], 0.15f, true);

        RigEffectState drawn = RigBlend.Add(base_, layer, 0.5f).Effects[0];

        Assert.That(drawn.Visible, Is.True);
        Assert.That(drawn.Alpha, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void TheRuntimePoseCarriesEffectsThroughPlayback()
    {
        var runtime = new AnimatorRuntime(Package());
        runtime.Play("attack", 0f);

        Assert.That(runtime.Pose.Effects, Has.Length.EqualTo(1));
        runtime.Update(0.15f);
        Assert.That(runtime.Pose.Effects[0].Visible, Is.True);
        runtime.Update(0.2f);
        Assert.That(runtime.Pose.Effects[0].Visible, Is.False);
    }

    [Test]
    public void AnEffectRidesItsBone()
    {
        RigPackage package = Package();
        package.Effects[0].Transform = new Transform2D(0f, new Vector2(5, 0));
        RigPose pose = RigSampler.Sample(package, package.Clips[1], 0f);

        Transform2D[] world = RigRig.WorldEffects(package, pose, RigRig.WorldBones(package, pose));

        Assert.That(world[0].Origin, Is.EqualTo(new Vector2(5, -40)));
    }

    [Test]
    public void ValidationRejectsEffectsThatCannotBeDrawnOrKeyed()
    {
        RigPackage package = Package();
        package.Effects.Add(
            new RigEffect
            {
                Id = "flash",
                BoneId = "missing",
                Item = new Xif
                {
                    Shaders = { XifShaderRef.OfMap(3) },
                    Primitives =
                        { new XifPrimitive { Kind = XifPrimitive.PolygonKind, Points = { Vector2.Zero, Vector2.One } } }
                }
            });
        RigAuthoring.SetChannel(package.Clips[0], RigChannelTarget.Bone, "root", RigChannelProperty.Opacity, 0f, 1f);
        RigAuthoring.SetChannel(package.Clips[0], RigChannelTarget.Effect, "ghost", RigChannelProperty.Visible, 0f, 1f);

        string issues = string.Join(" ", RigValidation.Validate(package));

        Assert.That(issues, Does.Contain("references missing bone 'missing'"));
        Assert.That(issues, Does.Contain("map shader slot"));
        Assert.That(issues, Does.Contain("fewer than three points"));
        Assert.That(issues, Does.Contain("which it does not have"));
        Assert.That(issues, Does.Contain("missing effect 'ghost'"));
    }

    [Test]
    public void RenamingABoneCarriesItsEffectsAndDeletingOneNeedsThemGone()
    {
        RigPackage package = Package();

        Assert.That(RigRigAuthoring.DeleteBone(package, 1), Is.False, "the streak still hangs off the arm");
        Assert.That(RigRigAuthoring.RenameBone(package, 1, "sleeve"), Is.True);
        Assert.That(package.Effects[0].BoneId, Is.EqualTo("sleeve"));

        Assert.That(RigEffects.Remove(package, 0), Is.True);
        Assert.That(package.Clips[0].Channels, Is.Empty, "its keys go with it");
        Assert.That(RigRigAuthoring.DeleteBone(package, 1), Is.True);
    }

    [Test]
    public void NewEffectsGetUniqueIdsAndDrawAboveTheArt()
    {
        RigPackage package = Package();
        package.Attachments.Add(new RigAttachment { SvgPath = "a.svg", Group = "a", BoneId = "arm", ZOrder = 4 });

        int index = RigEffects.Add(package, "arm", RigEffects.Streak(), "streak");

        Assert.That(package.Effects[index].Id, Is.EqualTo("streak-2"));
        Assert.That(package.Effects[index].ZOrder, Is.EqualTo(5));
    }

    private static RigEffectState Sample(RigPackage package, float time) =>
        RigSampler.Sample(package, package.Clips[0], time).Effects[0];

    /// <summary>
    /// A root and an arm 40 up; a streak on the arm that "attack" shows from
    /// 0.1 s to 0.25 s, and an "idle" that never shows it.
    /// </summary>
    private static RigPackage Package()
    {
        var package = new RigPackage();
        package.Skeleton.Bones.Add(new RigBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Skeleton.Bones.Add(
            new RigBone { Id = "arm", Parent = 0, Rest = new Transform2D(0f, new Vector2(0, -40)) });
        package.Effects.Add(new RigEffect { Id = "streak", BoneId = "arm", Item = RigEffects.Streak() });

        var attack = new RigClip { Id = "attack", Duration = 0.5f };
        RigAuthoring.KeyEffectVisible(package, attack, 0, 0.1f, true);
        RigAuthoring.KeyEffectVisible(package, attack, 0, 0.25f, false);
        package.Clips.Add(attack);
        package.Clips.Add(new RigClip { Id = "idle", Duration = 1f });
        return package;
    }
}
