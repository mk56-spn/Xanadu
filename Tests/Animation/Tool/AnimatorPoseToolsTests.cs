using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorPoseToolsTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorPreviewInputSystem(Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private void Run(ToolActionKind kind) =>
        AnimatorCommands.Run(Session, new ToolAction(kind));

    private XafChannel? Channel(string bone, XafChannelProperty property) =>
        Idle.Channels.Find(channel => channel.TargetId == bone && channel.Property == property);

    private void Tap(params Key[] keys)
    {
        foreach (Key key in keys)
            Keyboard.Press(key);
        Frame();
        Keyboard.Clear();
        Frame();
    }

    private void Drag(Vector2 from, Vector2 to)
    {
        Pointer.Position = from;
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        Pointer.Position = to;
        Frame();
        Pointer.Pressed = false;
        Ui.Preview.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void KeyPoseKeysEveryBoneAtThePlayhead()
    {
        Runtime.Seek(0.5f);
        Runtime.Pose.Bones[1].Rotation = 0.3f;

        Run(ToolActionKind.KeyPose);

        foreach (string bone in new[] { "root", "torso", "head" })
            Assert.That(Channel(bone, XafChannelProperty.Rotation)!.Keyframes.Exists(key => key.Time == 0.5f), Is.True, bone);
        Assert.That(XafSampler.Sample(Package, Idle, 0.5f).Bones[1].Rotation, Is.EqualTo(0.3f).Within(0.0001f));
    }

    [Test]
    public void APastedPoseCanBeKeyedAtAnotherTime()
    {
        Runtime.Seek(0.5f);
        Runtime.Pose.Bones[1].Rotation = 0.4f;
        Tap(Key.Ctrl, Key.C);

        Runtime.Seek(1.5f);
        Tap(Key.Ctrl, Key.V);
        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(0.4f));

        Run(ToolActionKind.KeyPose);
        Assert.That(XafSampler.Sample(Package, Idle, 1.5f).Bones[1].Rotation, Is.EqualTo(0.4f).Within(0.0001f));
    }

    [Test]
    public void PastingWithAutoKeyKeysThePose()
    {
        Runtime.Pose.Bones[1].Rotation = 0.2f;
        Run(ToolActionKind.CopyPose);
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        Runtime.Seek(1f);

        Run(ToolActionKind.PastePose);

        Assert.That(Channel("torso", XafChannelProperty.Rotation)!.Keyframes.Exists(key => key.Time == 1f), Is.True);
    }

    [Test]
    public void PastingBeforeCopyingChangesNothing()
    {
        Run(ToolActionKind.PastePose);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("COPY A POSE FIRST"));
    }

    [Test]
    public void IkDragPutsTheJointOnThePointerAndKeepsBoneLengths()
    {
        Session.GetComponent<AnimatorModes>().Ik = true;
        Vector2 target = PreviewOrigin() + new Vector2(60, -100);

        Pointer.Position = BoneScreenPosition(2);
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();
        Pointer.Position = target;
        Frame();

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Ik));
        Transform2D[] world = XafRig.WorldBones(Package, Runtime.Pose);
        Assert.That(world[2].Origin.DistanceTo(target - PreviewOrigin()), Is.LessThan(0.01f));
        Assert.That(world[1].Origin.DistanceTo(world[0].Origin), Is.EqualTo(80f).Within(0.01f));
        Assert.That(world[2].Origin.DistanceTo(world[1].Origin), Is.EqualTo(70f).Within(0.01f));
    }

    [Test]
    public void IkDragWithAutoKeyKeysTheBentBones()
    {
        Session.GetComponent<AnimatorModes>().Ik = true;
        Session.GetComponent<AnimatorModes>().AutoKey = true;

        Drag(BoneScreenPosition(2), PreviewOrigin() + new Vector2(60, -100));

        Assert.That(Channel("root", XafChannelProperty.Rotation), Is.Not.Null);
        Assert.That(Channel("torso", XafChannelProperty.Rotation), Is.Not.Null);
    }

    [Test]
    public void IkNeedsTwoBonesAboveTheJoint()
    {
        Session.GetComponent<AnimatorModes>().Ik = true;
        Pointer.Position = BoneScreenPosition(1);
        Pointer.Pressed = true;
        Ui.Preview.AddTag<Pressed>();
        Frame();

        // The torso only has the root above it, so it swings the root as usual.
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.Swing));
    }

    [Test]
    public void SpaceTogglesPlayback()
    {
        Runtime.SetPaused(true);

        Tap(Key.Space);
        Assert.That(Runtime.Paused, Is.False);

        Tap(Key.Space);
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void ArrowsStepThePlayheadAndHomeEndJumpToTheEnds()
    {
        Runtime.Seek(0f);

        Tap(Key.Right);
        Tap(Key.Right);
        float stepped = Runtime.CurrentTime;
        Assert.That(stepped, Is.GreaterThan(0f));
        Tap(Key.Left);
        Assert.That(Runtime.CurrentTime, Is.LessThan(stepped));
        Assert.That(Runtime.Paused, Is.True);

        Tap(Key.End);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(Idle.Duration));
        Tap(Key.Home);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0f));
    }

    [Test]
    public void UpAndDownJumpBetweenKeys()
    {
        // The idle clip has head keys at 0, 1 and 2 seconds.
        Runtime.Seek(0.4f);

        Tap(Key.Down);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(1f));
        Tap(Key.Down);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(2f));
        Tap(Key.Up);
        Tap(Key.Up);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0f));
    }

    [Test]
    public void ShortcutsAreIgnoredWhileTyping()
    {
        Runtime.SetPaused(true);
        Ui.ClipName.AddTag<TextFieldFocused>();

        Tap(Key.Space);

        Assert.That(Runtime.Paused, Is.True);
    }
}

[TestFixture]
public sealed class AnimatorMirrorPoseTests : AnimatorToolTest
{
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        package.Skeleton.Bones.Add(new XafBone { Id = "arm_l", Parent = 1, Rest = new Transform2D(0f, new Vector2(-30, -60)) });
        package.Skeleton.Bones.Add(new XafBone { Id = "arm_r", Parent = 1, Rest = new Transform2D(0f, new Vector2(30, -60)) });
        return package;
    }

    [Test]
    public void MirroringSwapsSidesAndReversesTurns()
    {
        Runtime.Pose.Bones[1].Rotation = 0.1f;
        Runtime.Pose.Bones[3].Rotation = 0.5f;
        Runtime.Pose.Bones[4].Rotation = -0.2f;
        Runtime.Pose.Bones[0].Translation = new Vector2(12, 4);

        AnimatorPoseCommands.MirrorPose(Session);

        Assert.That(Runtime.Pose.Bones[1].Rotation, Is.EqualTo(-0.1f));
        Assert.That(Runtime.Pose.Bones[3].Rotation, Is.EqualTo(0.2f));
        Assert.That(Runtime.Pose.Bones[4].Rotation, Is.EqualTo(-0.5f));
        Assert.That(Runtime.Pose.Bones[0].Translation, Is.EqualTo(new Vector2(-12, 4)));
        // Fixed-length arms keep their own side's offset.
        Assert.That(Runtime.Pose.Bones[3].Translation, Is.EqualTo(new Vector2(-30, -60)));
    }

    [Test]
    public void MirroringTwiceRestoresThePose()
    {
        Runtime.Pose.Bones[3].Rotation = 0.5f;
        Runtime.Pose.Bones[4].Rotation = -0.2f;

        AnimatorPoseCommands.MirrorPose(Session);
        AnimatorPoseCommands.MirrorPose(Session);

        Assert.That(Runtime.Pose.Bones[3].Rotation, Is.EqualTo(0.5f));
        Assert.That(Runtime.Pose.Bones[4].Rotation, Is.EqualTo(-0.2f));
    }

    [TestCase("arm_l", "arm_r")]
    [TestCase("Hand.R", "Hand.L")]
    [TestCase("torso", "torso")]
    public void TwinNames(string id, string twin) =>
        Assert.That(AnimatorPoseCommands.MirrorId(id), Is.EqualTo(twin));
}
