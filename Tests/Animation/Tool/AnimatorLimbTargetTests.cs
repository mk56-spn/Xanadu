using Xanadu.Serialisation.Animation;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Tools.Animator.Ui;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorLimbTargetTests : AnimatorToolTest
{
    private const int RootBone = 0;
    private const int Foot = 4;

    protected override void AddSystems()
    {
        Root.Add(new AnimatorPreviewInputSystem(Pointer));
        Root.Add(new AnimatorPlantSystem());
    }

    protected override RigPackage CreatePackage()
    {
        var package = new RigPackage();
        Add(package, "root", -1, Vector2.Zero);
        Add(package, "hip", 0, new Vector2(0, -100));
        Add(package, "thigh", 1, Vector2.Zero);
        Add(package, "shin", 2, new Vector2(4, 48));
        Add(package, "foot", 3, new Vector2(-4, 48));
        package.Clips.Add(new RigClip { Id = "idle", Duration = 1f });
        package.Clips.Add(new RigClip { Id = "other", Duration = 1f });
        return package;
    }

    private static void Add(RigPackage package, string id, int parent, Vector2 offset) =>
        package.Skeleton.Bones.Add(new RigBone { Id = id, Parent = parent, Rest = new Transform2D(0f, offset) });

    private RigClip Clip => Package.Clips[0];

    private void Select(int bone) => Session.GetComponent<AnimatorSelection>().SelectOnlyBone(bone);

    private void Verb(string verb) => AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.LimbTarget, verb));

    private Vector2 Where(int bone, float time) =>
        RigRig.WorldBones(Package, RigSampler.Sample(Package, Clip, time))[bone].Origin;

    private string Status => Session.GetComponent<AnimatorStatus>().Message;

    private RigLimbTarget Orbit()
    {
        Select(Foot);
        Verb("orbit");
        return Clip.Targets[0];
    }

    [Test]
    public void AnOrbitStartsWhereTheBoneIsAndTravelsWithTheBoneAboveTheLimb()
    {
        Vector2 start = Where(Foot, 0f);
        RigLimbTarget target = Orbit();

        Assert.That(target.AnchorId, Is.EqualTo("hip"));
        Assert.That((target.From, target.To), Is.EqualTo((0f, 1f)));
        Assert.That(Where(Foot, 0f).DistanceTo(start), Is.LessThan(0.2f));
        Assert.That(Where(Foot, 0.5f).DistanceTo(start), Is.GreaterThan(5f), "it goes somewhere");
    }

    [Test]
    public void ANewOrbitOnAStraightLimbIsAllWithinReach()
    {
        RigLimbTarget target = Orbit();
        for (int i = 0; i <= 40; i++)
        {
            float time = i / 40f;
            Transform2D[] world = RigRig.WorldBones(Package, RigSampler.Sample(Package, Clip, time));
            Assert.That(
                world[Foot].Origin.DistanceTo(RigTargets.WorldAt(Package, target, world, time)),
                Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.3f),
                $"at {time}");
        }
    }

    [Test]
    public void ALoopingClipOrbitsItsLoopRange()
    {
        Clip.Loop = true;
        Clip.LoopStart = 0.2f;
        Clip.LoopEnd = 0.8f;
        RigLimbTarget target = Orbit();
        Assert.That((target.From, target.To), Is.EqualTo((0.2f, 0.8f)));
    }

    [Test]
    public void TheRootOrbitsByMoving()
    {
        Select(RootBone);
        Verb("orbit");
        Assert.That(Clip.Channels.TrueForAll(channel => channel.TargetId == "root"), Is.True);
        Assert.That(Clip.Targets[0].AnchorId, Is.EqualTo(""));
    }

    [Test]
    public void ABoneWithNoLimbAndNoFreedomIsRefused()
    {
        Select(1);
        Verb("orbit");
        Assert.That(Clip.Targets, Is.Empty);
        Assert.That(Status, Does.Contain("SELECT"));
    }

    [Test]
    public void ASecondTargetOverTheSameTimeIsRefused()
    {
        Orbit();
        Verb("orbit");
        Assert.That(Clip.Targets, Has.Count.EqualTo(1));
        Assert.That(Status, Does.Contain("ALREADY"));
    }

    [Test]
    public void ChangingTheShapeReBakesAndIsOneUndoStep()
    {
        RigLimbTarget target = Orbit();

        Vector2 before = Where(Foot, 0.25f);
        float radius = target.RadiusX;

        Verb("rx:3");
        Assert.That(Clip.Targets[0].RadiusX, Is.EqualTo(radius + 6f));
        Assert.That(Where(Foot, 0.25f).DistanceTo(before), Is.GreaterThan(1f));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Assert.That(Package.Clips[0].Targets[0].RadiusX, Is.EqualTo(radius));
        Assert.That(Where(Foot, 0.25f).DistanceTo(before), Is.LessThan(0.01f));
    }

    [Test]
    public void AnEditThatChangesNothingLeavesNoUndoStep()
    {
        Orbit();
        int steps = Session.GetComponent<AnimatorSessionState>().History.History.UndoCount;
        AnimatorHistoryCommands.Edit(Session, () => { });
        Assert.That(Session.GetComponent<AnimatorSessionState>().History.History.UndoCount, Is.EqualTo(steps));
    }

    [Test]
    public void KeysOnADrivenLimbAreOverwrittenAndItSaysSo()
    {
        Orbit();
        float value = Clip.Channels.Find(c => c.TargetId == "shin")!.Keyframes[0].Value;
        Session.GetComponent<AnimatorDocument>().Runtime.Seek(0f);

        AnimatorHistoryCommands.Edit(
            Session,
            () => RigAuthoring.SetChannel(
                Clip,
                RigChannelTarget.Bone,
                "shin",
                RigChannelProperty.Rotation,
                0f,
                value + 1f));

        Assert.That(Status, Does.Contain("BELONG"));
        Assert.That(
            Clip.Channels.Find(c => c.TargetId == "shin")!.Keyframes[0].Value,
            Is.EqualTo(value).Within(0.001f));
    }

    [Test]
    public void AnUpstreamEditIsFollowedWithoutComplaint()
    {
        RigLimbTarget target = Orbit();
        target.AnchorId = "";
        target.Center = Where(Foot, 0f) - new Vector2(target.RadiusX, 0f);
        Verb("rx:0");

        AnimatorHistoryCommands.Edit(
            Session,
            () =>
            {
                RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "root", RigChannelProperty.TranslationY, 0f, 0f);
                RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "root", RigChannelProperty.TranslationY, 1f, 25f);
                AnimatorSession.SetStatus(Session, "ROOT KEYED");
            });

        Assert.That(Status, Is.EqualTo("ROOT KEYED"));
        Assert.That(
            Where(Foot, 0.5f).DistanceTo(Clip.Targets[0].LocalAt(0.5f)),
            Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.3f));
    }

    [Test]
    public void DetachKeepsTheKeysAndStopsDriving()
    {
        Orbit();
        int keys = Clip.Channels.Find(c => c.TargetId == "shin")!.Keyframes.Count;

        Verb("detach");

        Assert.That(Clip.Targets, Is.Empty);
        Assert.That(Clip.Channels.Find(c => c.TargetId == "shin")!.Keyframes, Has.Count.EqualTo(keys));

        AnimatorHistoryCommands.Edit(
            Session,
            () => RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "shin", RigChannelProperty.Rotation, 0.5f, 2f));
        Assert.That(Status, Does.Not.Contain("BELONG"));
    }

    [Test]
    public void AnOverrideTooShortToSeeIsDroppedAndItsKeysAreOrdinary()
    {
        RigLimbTarget target = Orbit();
        // As a file can hold: a sliver far shorter than any span the tool makes.
        target.From = 0.5f;
        target.To = 0.5007f;

        AnimatorHistoryCommands.Edit(
            Session,
            () => RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "shin", RigChannelProperty.Rotation, 0.5f, 2f));

        Assert.That(Clip.Targets, Is.Empty);
        Assert.That(Status, Does.Not.Contain("BELONG"));
        Assert.That(Clip.Channels.Find(c => c.TargetId == "shin")!.Keyframes.Exists(k => k.Value == 2f), Is.True);
    }

    [Test]
    public void TheSpanCannotBeSetShorterThanTheShortestOverride()
    {
        Orbit();
        Session.GetComponent<AnimatorDocument>().Runtime.Seek(0.999f);
        Verb("from");
        Assert.That(Clip.Targets[0].From, Is.EqualTo(0f));
    }

    [Test]
    public void AnchorCyclesAndTheStartStaysPut()
    {
        Orbit();
        Vector2 start = Where(Foot, 0f);
        Verb("anchor");
        Assert.That(Clip.Targets[0].AnchorId, Is.EqualTo("root"));
        Assert.That(Where(Foot, 0f).DistanceTo(start), Is.LessThan(0.2f));
        Verb("anchor");
        Assert.That(Clip.Targets[0].AnchorId, Is.EqualTo(""));
        Assert.That(Where(Foot, 0f).DistanceTo(start), Is.LessThan(0.2f));
    }

    [Test]
    public void TheSpanSetsFromThePlayheadAndCannotCross()
    {
        Orbit();
        Session.GetComponent<AnimatorDocument>().Runtime.Seek(0.25f);
        Verb("from");
        Assert.That(Clip.Targets[0].From, Is.EqualTo(0.25f));

        Session.GetComponent<AnimatorDocument>().Runtime.Seek(0.1f);
        Verb("to");
        Assert.That(Clip.Targets[0].To, Is.EqualTo(1f));
    }

    [Test]
    public void SavingBakesEveryClipAndKeepsTheTarget()
    {
        RigClip other = Package.Clips[1];
        other.Targets.Add(
            new RigLimbTarget { BoneId = "foot", Shape = RigTargetShape.Spot, Center = new Vector2(0, -4), To = 1f });

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Save));

        Assert.That(other.Channels.Exists(c => c.TargetId == "shin"), Is.True);
        Assert.That(RigBinary.Read(RigBinary.Write(Files.Saved!)).Clips[1].Targets, Has.Count.EqualTo(1));
    }

    [Test]
    public void AReleasedPlantBecomesATargetThatSurvivesReTimingTheBody()
    {
        AnimatorDocument document = Session.GetComponent<AnimatorDocument>();
        document.Runtime.Seek(0f);
        Select(Foot);
        Vector2 spot = Where(Foot, 0f);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PlantBone));
        Frame();
        document.Runtime.Seek(1f);
        Frame();
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.PlantBone));
        Frame();

        Assert.That(Clip.Targets[0].Shape, Is.EqualTo(RigTargetShape.Spot));

        // The body moves after the plant was released, and the foot still holds.
        AnimatorHistoryCommands.Edit(
            Session,
            () =>
            {
                RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "root", RigChannelProperty.TranslationY, 0f, 0f);
                RigAuthoring.SetChannel(Clip, RigChannelTarget.Bone, "root", RigChannelProperty.TranslationY, 1f, 25f);
            });
        Assert.That(Where(Foot, 0.6f).DistanceTo(spot), Is.LessThan(AnimatorPlantCommands.DriftTolerance + 0.3f));
    }

    private Vector2 Screen(RigLimbTarget target, Vector2 local)
    {
        Transform2D placement = AnimatorGeometry.PreviewPlacement(
            AnimatorGeometry.PanelArea(Ui.Preview),
            Session.GetComponent<AnimatorPreviewView>());
        Transform2D[] world = RigRig.WorldBones(Package, Runtime.Pose);
        return placement * AnimatorTargetCommands.AnchorWorld(Package, target, world) * local;
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
    public void DraggingTheCentreHandleMovesTheEllipseAsOneUndoStep()
    {
        RigLimbTarget target = Orbit();
        Runtime.Seek(0f);
        Runtime.SetPaused(true);
        Vector2 center = target.Center;
        int steps = Session.GetComponent<AnimatorSessionState>().History.History.UndoCount;

        Drag(Screen(target, center), Screen(target, center) + new Vector2(30f, 0f));

        Assert.That(Clip.Targets[0].Center.X, Is.GreaterThan(center.X + 1f));
        Assert.That(Session.GetComponent<AnimatorSessionState>().History.History.UndoCount, Is.EqualTo(steps + 1));
    }

    [Test]
    public void DraggingTheLongHandleSizesAndTiltsIt()
    {
        RigLimbTarget target = Orbit();
        Runtime.Seek(0f);
        Runtime.SetPaused(true);
        Vector2 handle = Screen(target, AnimatorTargetCommands.RadiusXHandle(target));

        Drag(handle, handle + new Vector2(0f, 40f));

        Assert.That(Clip.Targets[0].Tilt, Is.Not.EqualTo(0f));
        Assert.That(Clip.Targets[0].RadiusX, Is.Not.EqualTo(AnimatorTargetCommands.DefaultRadiusX));
    }

    [Test]
    public void ReachIsMeasuredFromTheTopOfTheLimb()
    {
        Transform2D[] world = RigRig.WorldBones(Package, Runtime.Pose);
        Vector2 top = world[2].Origin;
        Assert.That(RigTargets.CanReach(Package, world, Foot, top + new Vector2(0f, 40f)), Is.True);
        Assert.That(RigTargets.CanReach(Package, world, Foot, top + new Vector2(0f, 200f)), Is.False);
        Assert.That(RigTargets.CanReach(Package, world, RootBone, new Vector2(900f, 900f)), Is.True);
    }

    [Test]
    public void AMissingBoneDoesNotBreakAnything()
    {
        Orbit();
        Clip.Targets[0].AnchorId = "gone";
        Verb("rx:1");
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Save));
        Frame();
        Assert.That(RigTargets.IsValid(Package, Clip.Targets[0]), Is.False);
    }
}
