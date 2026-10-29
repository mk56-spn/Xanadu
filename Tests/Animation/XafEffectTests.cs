using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Items;

namespace Xanadu.Tests.Animation;

/// <summary>Effects: Xif items on bones that clips show, hide, fade and move.</summary>
[TestFixture]
public sealed class XafEffectTests
{
    [Test]
    public void AnEffectAndItsPolygonRoundTripThroughTheBinaryFormat()
    {
        XafPackage package = Package();
        XafEffect effect = package.Effects[0];
        effect.Transform = new Transform2D(0.5f, new Vector2(10, -4));
        effect.ZOrder = 7;
        effect.Item.Primitives[0].Ref = "res://streak.png";
        effect.Item.Shaders.Add(XifShaderRef.OfCommon("res://Assets/Shaders/Items/rainbow.gdshader"));

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package));

        Assert.That(XafPackageCopy.AreEqual(package, loaded), Is.True);
        XifPrimitive polygon = loaded.Effects[0].Item.Primitives[0];
        Assert.That(polygon.Kind, Is.EqualTo(XifPrimitive.PolygonKind));
        Assert.That(polygon.Points, Is.EqualTo(XafEffects.StreakOutline()));
        Assert.That(polygon.Fill, Is.EqualTo(new Color(1f, 1f, 1f, 0.85f)));
        Assert.That(loaded.Effects[0].ZOrder, Is.EqualTo(7));
    }

    [Test]
    public void FilesFromBeforeEffectsReadWithNone()
    {
        XafPackage package = Package();
        package.Effects.Clear();
        package.Clips[0].Channels.Clear();

        XafPackage loaded = XafBinary.Read(XafBinary.Write(package, 4));

        Assert.That(loaded.Effects, Is.Empty);
    }

    [Test]
    public void AnEffectIsHiddenUntilAClipShowsItAndHeldBetweenKeys()
    {
        XafPackage package = Package();

        Assert.That(Sample(package, 0.05f).Visible, Is.False);
        Assert.That(Sample(package, 0.1f).Visible, Is.True, "shown from its key exactly");
        Assert.That(Sample(package, 0.24f).Visible, Is.True, "held, not blended towards the hide key");
        Assert.That(Sample(package, 0.25f).Visible, Is.False);
        Assert.That(Sample(package, 0.1f).Alpha, Is.EqualTo(1f));
    }

    [Test]
    public void OpacityAndTransformKeysMoveTheEffect()
    {
        XafPackage package = Package();
        XafClip clip = package.Clips[0];
        XafAuthoring.KeyEffect(package, clip, 0, 0.1f, new XafEffectState
        {
            Transform = new XafTransform { Translation = new Vector2(0, 0), Scale = Vector2.One },
            Opacity = 1f,
        });
        XafAuthoring.KeyEffect(package, clip, 0, 0.2f, new XafEffectState
        {
            Transform = new XafTransform { Translation = new Vector2(40, 0), Scale = Vector2.One },
            Opacity = 0f,
        });

        XafEffectState middle = Sample(package, 0.15f);

        Assert.That(middle.Opacity, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(middle.Alpha, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(middle.Transform.Translation.X, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void ACrossfadeFadesAnEffectOutWithTheClipThatShowedIt()
    {
        XafPackage package = Package();
        XafPose shown = XafSampler.Sample(package, package.Clips[0], 0.15f);
        XafPose hidden = XafSampler.Sample(package, package.Clips[1], 0f);

        XafEffectState blended = XafBlend.Crossfade(shown, hidden, 0.25f).Effects[0];

        Assert.That(blended.Alpha, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(blended.Transform.Translation, Is.EqualTo(shown.Effects[0].Transform.Translation),
            "it stays where the clip showing it put it");
    }

    [Test]
    public void AnAdditiveLayerDrawsItsEffectOverTheBaseAtItsWeight()
    {
        XafPackage package = Package();
        XafPose base_ = XafSampler.Sample(package, package.Clips[1], 0f);
        XafPose layer = XafSampler.Sample(package, package.Clips[0], 0.15f, additive: true);

        XafEffectState drawn = XafBlend.Add(base_, layer, 0.5f).Effects[0];

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
        XafPackage package = Package();
        package.Effects[0].Transform = new Transform2D(0f, new Vector2(5, 0));
        XafPose pose = XafSampler.Sample(package, package.Clips[1], 0f);

        Transform2D[] world = XafRig.WorldEffects(package, pose, XafRig.WorldBones(package, pose));

        Assert.That(world[0].Origin, Is.EqualTo(new Vector2(5, -40)));
    }

    [Test]
    public void ValidationRejectsEffectsThatCannotBeDrawnOrKeyed()
    {
        XafPackage package = Package();
        package.Effects.Add(new XafEffect
        {
            Id = "flash",
            BoneId = "missing",
            Item = new Xif
            {
                Shaders = { XifShaderRef.OfMap(3) },
                Primitives = { new XifPrimitive { Kind = XifPrimitive.PolygonKind, Points = { Vector2.Zero, Vector2.One } } },
            },
        });
        XafAuthoring.SetChannel(package.Clips[0], XafChannelTarget.Bone, "root", XafChannelProperty.Opacity, 0f, 1f);
        XafAuthoring.SetChannel(package.Clips[0], XafChannelTarget.Effect, "ghost", XafChannelProperty.Visible, 0f, 1f);

        string issues = string.Join(" ", XafValidation.Validate(package));

        Assert.That(issues, Does.Contain("references missing bone 'missing'"));
        Assert.That(issues, Does.Contain("map shader slot"));
        Assert.That(issues, Does.Contain("fewer than three points"));
        Assert.That(issues, Does.Contain("which only effects have"));
        Assert.That(issues, Does.Contain("missing effect 'ghost'"));
    }

    [Test]
    public void RenamingABoneCarriesItsEffectsAndDeletingOneNeedsThemGone()
    {
        XafPackage package = Package();

        Assert.That(XafRigAuthoring.DeleteBone(package, 1), Is.False, "the streak still hangs off the arm");
        Assert.That(XafRigAuthoring.RenameBone(package, 1, "sleeve"), Is.True);
        Assert.That(package.Effects[0].BoneId, Is.EqualTo("sleeve"));

        Assert.That(XafEffects.Remove(package, 0), Is.True);
        Assert.That(package.Clips[0].Channels, Is.Empty, "its keys go with it");
        Assert.That(XafRigAuthoring.DeleteBone(package, 1), Is.True);
    }

    [Test]
    public void NewEffectsGetUniqueIdsAndDrawAboveTheArt()
    {
        XafPackage package = Package();
        package.Attachments.Add(new XafAttachment { SvgPath = "res://a.svg", Group = "a", BoneId = "arm", ZOrder = 4 });

        int index = XafEffects.Add(package, "arm", XafEffects.Streak(), "streak");

        Assert.That(package.Effects[index].Id, Is.EqualTo("streak-2"));
        Assert.That(package.Effects[index].ZOrder, Is.EqualTo(5));
    }

    private static XafEffectState Sample(XafPackage package, float time) =>
        XafSampler.Sample(package, package.Clips[0], time).Effects[0];

    /// <summary>
    /// A root and an arm 40 up; a streak on the arm that "attack" shows from
    /// 0.1 s to 0.25 s, and an "idle" that never shows it.
    /// </summary>
    private static XafPackage Package()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "root", Parent = -1, Rest = Transform2D.Identity });
        package.Skeleton.Bones.Add(new XafBone { Id = "arm", Parent = 0, Rest = new Transform2D(0f, new Vector2(0, -40)) });
        package.Effects.Add(new XafEffect { Id = "streak", BoneId = "arm", Item = XafEffects.Streak() });

        var attack = new XafClip { Id = "attack", Duration = 0.5f };
        XafAuthoring.KeyEffectVisible(package, attack, 0, 0.1f, true);
        XafAuthoring.KeyEffectVisible(package, attack, 0, 0.25f, false);
        package.Clips.Add(attack);
        package.Clips.Add(new XafClip { Id = "idle", Duration = 1f });
        return package;
    }
}
