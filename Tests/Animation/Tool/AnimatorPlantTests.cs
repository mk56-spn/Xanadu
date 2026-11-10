using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorPlantTests : AnimatorToolTest
{
    private const int Root = 0;
    private const int Thigh = 2;
    private const int Shin = 3;
    private const int Foot = 4;

    protected override void AddSystems() => base.Root.Add(new AnimatorPlantSystem());

    /// <summary>A hip over a slightly bent leg, so it has room to fold; "idle" lasts a second and keys only the root.</summary>
    protected override XafPackage CreatePackage()
    {
        var package = new XafPackage();
        Add(package, "root", -1, Vector2.Zero);
        Add(package, "hip", 0, new Vector2(0, -100));
        Add(package, "thigh", 1, Vector2.Zero);
        Add(package, "shin", 2, new Vector2(4, 48));
        Add(package, "foot", 3, new Vector2(-4, 48));
        var clip = new XafClip { Id = "idle", Duration = 1f };
        var down = new XafChannel { Target = XafChannelTarget.Bone, TargetId = "root", Property = XafChannelProperty.TranslationY };
        down.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        down.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 30f });
        clip.Channels.Add(down);
        package.Clips.Add(clip);
        package.Clips.Add(new XafClip { Id = "other", Duration = 1f });
        return package;
    }

    private static void Add(XafPackage package, string id, int parent, Vector2 offset) =>
        package.Skeleton.Bones.Add(new XafBone { Id = id, Parent = parent, Rest = new Transform2D(0f, offset) });

    private Vector2 FootAt => XafRig.WorldBones(Package, Runtime.Pose)[Foot].Origin;

    private float FootFacing => XafRig.WorldBones(Package, Runtime.Pose)[Foot].Rotation;

    private void Toggle(int bone)
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(bone);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PlantBone));
        Frame();
    }

    private System.Collections.Generic.List<AnimatorPlant> Held => Session.GetComponent<AnimatorPlants>().Held;

    [Test]
    public void APlantedFootStaysPutAndFacesTheSameWayWhenTheBodyMoves()
    {
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        float facing = FootFacing;

        Runtime.Pose.Bones[Root].Translation = new Vector2(12f, 25f);
        Runtime.Pose.Bones[Root].Rotation = 0.2f;
        Frame();

        Assert.That(FootAt.DistanceTo(spot), Is.LessThan(0.05f));
        Assert.That(FootFacing, Is.EqualTo(facing).Within(0.001f));
    }

    [Test]
    public void TurningAPinnedFootTurnsItsPinAndStays()
    {
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        float facing = FootFacing;
        int footKeys = Keys("foot", XafChannelProperty.Rotation);

        AnimatorTransformCommands.SetRotation(Session, Runtime.Pose.Bones[Foot].Rotation + 0.4f);
        Frame();

        XafLimbTarget pin = Idle.Targets[0];
        Assert.That(pin.Facing, Is.EqualTo(facing + 0.4f).Within(0.001f));
        Assert.That(FootFacing, Is.EqualTo(facing + 0.4f).Within(0.001f));
        Assert.That(FootAt.DistanceTo(spot), Is.LessThan(0.05f));

        // The pin owns the foot's keys: it re-bakes them, and the turn holds across its span.
        Runtime.Seek(0.7f);
        Frame();
        Assert.That(FootFacing, Is.EqualTo(facing + 0.4f).Within(0.001f));
        Assert.That(Keys("foot", XafChannelProperty.Rotation), Is.EqualTo(footKeys));
    }

    [Test]
    public void DraggingAPinnedFootsStemTurnsItsPin()
    {
        Runtime.Seek(0f);
        Toggle(Foot);
        float facing = FootFacing;
        Session.GetComponent<AnimatorModes>().Handles = true;
        Session.GetComponent<AnimatorModes>().Svg = false;

        Transform2D placement = Transform2D.Identity;
        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Vector2 joint = world[Foot].Origin;
        Vector2 stem = AnimatorTipHandles.Tip(Package, Foot, world, placement);
        Assert.That(AnimatorTipHandles.TryBegin(Session, stem, placement, Runtime.Pose, world), Is.True);
        AnimatorTipHandles.Drag(Session, joint + (stem - joint).Rotated(Mathf.Pi / 6f), placement, free: true);
        // Ending the drag keys nothing: the pin owns the foot's keys.
        Assert.That(Session.GetComponent<AnimatorDrag>().Repinned, Is.True);
        AnimatorHistoryCommands.EndGesture(Session, null);
        Frame();

        Assert.That(Idle.Targets[0].Facing, Is.EqualTo(facing + Mathf.Pi / 6f).Within(0.001f));
        Assert.That(FootFacing, Is.EqualTo(facing + Mathf.Pi / 6f).Within(0.001f));
    }

    [Test]
    public void TheFootStaysPutWhileTheClipScrubs()
    {
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);

        Runtime.Seek(0.7f);
        Frame();

        Assert.That(FootAt.DistanceTo(spot), Is.LessThan(0.05f));
    }

    [Test]
    public void TheBakedLimbHoldsTheFootWithNothingSolving()
    {
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        Runtime.SetPaused(false);
        Frame();
        foreach (float time in new[] { 0f, 0.113f, 0.5f, 0.777f, 1f })
        {
            Runtime.Seek(time);
            Assert.That(FootAt.DistanceTo(spot), Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.05f), $"at {time}");
        }
    }

    private int Keys(string bone, XafChannelProperty property) =>
        Idle.Channels.Find(c => c.TargetId == bone && c.Property == property)?.Keyframes.Count ?? 0;

    [Test]
    public void AGentleMoveNeedsFarFewerKeysThanMoments()
    {
        Runtime.Seek(0f);
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        // One second is 61 moments; the body only sinks a little in a straight line.
        Assert.That(Keys("shin", XafChannelProperty.Rotation), Is.InRange(2, 8));
        Assert.That(Keys("thigh", XafChannelProperty.Rotation), Is.EqualTo(Keys("shin", XafChannelProperty.Rotation)));
    }

    [Test]
    public void PressingAgainAtItsFirstMomentUnplantsAndKeepsTheKeys()
    {
        Runtime.Seek(0.5f);
        Toggle(Foot);
        Toggle(Foot);

        Assert.That(Idle.Targets, Is.Empty);
        Assert.That(Held, Is.Empty);
        Assert.That(Keys("shin", XafChannelProperty.Rotation), Is.GreaterThan(0));
    }

    [Test]
    public void OnlyRotationIsKeyed()
    {
        Runtime.Seek(0f);
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        foreach (XafChannel channel in Idle.Channels)
        {
            if (channel.TargetId is "thigh" or "shin" or "foot")
                Assert.That(channel.Property, Is.EqualTo(XafChannelProperty.Rotation));
        }
    }

    [Test]
    public void AFastSwingStillHoldsTheFootByAddingKeysOnlyWhereNeeded()
    {
        // The hip sinks first, then swings over the foot: bent throughout, so always in reach.
        XafChannel across = new() { Target = XafChannelTarget.Bone, TargetId = "root", Property = XafChannelProperty.TranslationX };
        across.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        across.Keyframes.Add(new XafKeyframe { Time = 0.25f, Value = 15f });
        across.Keyframes.Add(new XafKeyframe { Time = 0.5f, Value = -15f });
        across.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 0f });
        Idle.Channels.Add(across);
        Idle.Channels[0].Keyframes.Insert(1, new XafKeyframe { Time = 0.25f, Value = 30f });
        Idle.Channels[0].Keyframes[2].Value = 30f;
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        foreach (float time in new[] { 0.1f, 0.25f, 0.4f, 0.5f, 0.66f, 0.9f })
        {
            Runtime.Seek(time);
            Assert.That(FootAt.DistanceTo(spot), Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.1f), $"at {time}");
        }
        Assert.That(Keys("shin", XafChannelProperty.Rotation), Is.LessThan(30));
        TestContext.Out.WriteLine($"KEYS {Keys("shin", XafChannelProperty.Rotation)}");
    }

    [Test]
    public void AHoldRunningToTheEndOfALoopHoldsUpToTheWrap()
    {
        Idle.Loop = true;
        Idle.LoopStart = 0f;
        Idle.LoopEnd = 1f;
        Runtime.Seek(0f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        foreach (float time in new[] { 0.5f, 0.9f, 0.97f, 0.995f })
        {
            Runtime.Seek(time);
            Assert.That(FootAt.DistanceTo(spot), Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.05f), $"at {time}");
        }
    }

    [Test]
    public void SpotsPastTheLegsReachDoNotAskForMoreKeys()
    {
        XafChannel across = new() { Target = XafChannelTarget.Bone, TargetId = "root", Property = XafChannelProperty.TranslationX };
        across.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        across.Keyframes.Add(new XafKeyframe { Time = 1f, Value = 300f });
        Idle.Channels.Add(across);
        Runtime.Seek(0f);
        Toggle(Foot);
        Runtime.Seek(1f);
        Frame();

        Toggle(Foot);

        Assert.That(Keys("shin", XafChannelProperty.Rotation), Is.LessThan(12));
    }

    [Test]
    public void UnplantingKeepsAnUnkeyedMoveAtThePlayhead()
    {
        Runtime.Seek(0.5f);
        Vector2 spot = FootAt;
        Toggle(Foot);
        Runtime.Pose.Bones[Root].Translation = new Vector2(10f, 20f);
        Frame();

        Toggle(Foot);

        Assert.That(Runtime.Pose.Bones[Root].Translation, Is.EqualTo(new Vector2(10f, 20f)));
        Assert.That(FootAt.DistanceTo(spot), Is.LessThan(0.05f));
    }

    [Test]
    public void APlantOverwritesLegKeysFromThePlayheadOnOnly()
    {
        var old = new XafChannel { Target = XafChannelTarget.Bone, TargetId = "shin", Property = XafChannelProperty.Rotation };
        old.Keyframes.Add(new XafKeyframe { Time = 0.103f, Value = 2f });
        old.Keyframes.Add(new XafKeyframe { Time = 0.9f, Value = 0f });
        Idle.Channels.Add(old);
        Runtime.Seek(0.5f);

        Toggle(Foot);

        XafChannel shin = Idle.Channels.Find(c => c.TargetId == "shin" && c.Property == XafChannelProperty.Rotation)!;
        Assert.That(shin.Keyframes.Exists(key => key.Time == 0.103f), Is.True);
        Assert.That(shin.Keyframes.Exists(key => key.Time == 0.9f), Is.False);
    }

    [Test]
    public void PlantingIsOneUndoStep()
    {
        Runtime.Seek(0f);
        int before = Idle.Channels.Count;

        Toggle(Foot);
        Assert.That(Idle.Channels.Count, Is.GreaterThan(before));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Frame();
        Assert.That(Package.Clips[0].Channels.Count, Is.EqualTo(before));
        Assert.That(Package.Clips[0].Targets, Is.Empty);
        Assert.That(Held, Is.Empty);
    }

    [Test]
    public void SelectingTheShinPlantsTheFootBelowIt()
    {
        Toggle(Shin);
        Assert.That(Idle.Targets[0].BoneId, Is.EqualTo("foot"));
        Assert.That(Held[0].Bone, Is.EqualTo(Foot));
    }

    [Test]
    public void ABoneWithNoLimbAboveItIsRefused()
    {
        Toggle(Root);
        Assert.That(Held, Is.Empty);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("PIN"));
    }

    [Test]
    public void TheRestPoseCannotPlant()
    {
        Session.GetComponent<AnimatorModes>().Rest = true;
        Toggle(Foot);
        Assert.That(Held, Is.Empty);
    }

    [Test]
    public void TooFarToReachIsFlagged()
    {
        Toggle(Foot);
        Runtime.Pose.Bones[Root].Translation = new Vector2(0f, -200f);
        Frame();
        Assert.That(Held[0].OutOfReach, Is.True);
    }

    [Test]
    public void APlantBelongsToItsClipAndIsThereOnComingBack()
    {
        Toggle(Foot);

        Runtime.Play("other", 0f);
        Runtime.SetPaused(true);
        Frame();
        Assert.That(Held, Is.Empty);

        Runtime.Play("idle", 0f);
        Runtime.SetPaused(true);
        Frame();
        Assert.That(Held, Has.Count.EqualTo(1));
    }

    [Test]
    public void APlantIsInTheSavedFileWithoutBeingReleased()
    {
        Runtime.Seek(0.25f);
        Vector2 spot = FootAt;
        Toggle(Foot);

        XafPackage saved = XafBinary.Read(XafBinary.Write(Package));

        XafLimbTarget plant = saved.Clips[0].Targets[0];
        Assert.That(plant.BoneId, Is.EqualTo("foot"));
        Assert.That(plant.Shape, Is.EqualTo(XafTargetShape.Spot));
        Assert.That(plant.Center.DistanceTo(spot), Is.LessThan(0.001f));
        Assert.That(plant.From, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(plant.To, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PressingAgainLaterEndsThePlantThere()
    {
        Runtime.Seek(0f);
        Toggle(Foot);
        Runtime.Seek(0.5f);
        Frame();

        Toggle(Foot);

        Assert.That(Idle.Targets[0].To, Is.EqualTo(0.5f).Within(0.001f));
        Runtime.Seek(0.7f);
        Frame();
        Assert.That(Held, Is.Empty);
    }

    [Test]
    public void APlantStopsShortOfTheBonesNextTarget()
    {
        Idle.Targets.Add(new XafLimbTarget { BoneId = "foot", Shape = XafTargetShape.Spot, Center = Vector2.Zero, From = 0.6f, To = 0.8f });
        Runtime.Seek(0.1f);

        Toggle(Foot);

        Assert.That(Idle.Targets, Has.Count.EqualTo(2));
        Assert.That(Idle.Targets[1].To, Is.EqualTo(0.6f).Within(0.001f));
    }

    [Test]
    public void NoTimeLeftInTheClipIsRefused()
    {
        Runtime.Seek(1f);
        Toggle(Foot);
        Assert.That(Idle.Targets, Is.Empty);
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("NO TIME"));
    }

}
