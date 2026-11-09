using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorOverrideTimelineTests : AnimatorToolTest
{
    private const int Foot = 4;

    protected override void AddSystems()
    {
        Root.Add(new Xanadu.Systems.Groups.TimelineSystemGroup(Pointer, Pointer));
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorKeyMenuSystem(Pointer));
    }

    protected override XafPackage CreatePackage()
    {
        var package = new XafPackage();
        Add(package, "root", -1, Vector2.Zero);
        Add(package, "hip", 0, new Vector2(0, -100));
        Add(package, "thigh", 1, Vector2.Zero);
        Add(package, "shin", 2, new Vector2(4, 48));
        Add(package, "foot", 3, new Vector2(-4, 48));
        var clip = new XafClip { Id = "idle", Duration = 2f };
        var down = new XafChannel { Target = XafChannelTarget.Bone, TargetId = "root", Property = XafChannelProperty.TranslationY };
        down.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        down.Keyframes.Add(new XafKeyframe { Time = 2f, Value = 30f });
        clip.Channels.Add(down);
        package.Clips.Add(clip);
        return package;
    }

    private static void Add(XafPackage package, string id, int parent, Vector2 offset) =>
        package.Skeleton.Bones.Add(new XafBone { Id = id, Parent = parent, Rest = new Transform2D(0f, offset) });

    private XafLimbTarget AddOverride(XafTargetShape shape, float from, float to)
    {
        var target = new XafLimbTarget { BoneId = "foot", Shape = shape, Center = new Vector2(0, 0), RadiusX = 5f, RadiusY = 5f, From = from, To = to };
        Idle.Targets.Add(target);
        XafTargets.Bake(Package, Idle);
        return target;
    }

    private Vector2 OnBar(float seconds) => new(Model().XForSeconds(seconds), Model().RowRect(0).GetCenter().Y);

    private void DragFromTo(float from, float to)
    {
        Pointer.Position = OnBar(from);
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
        Pointer.Position = OnBar(to);
        Frame();
        Pointer.Pressed = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    private void RightClickBar(float seconds)
    {
        Pointer.Position = OnBar(seconds);
        Pointer.Secondary = true;
        Frame();
        Pointer.Secondary = false;
        Frame();
    }

    private void Choose(ToolActionKind kind)
    {
        Entity option = Button(kind);
        Pointer.Pressed = true;
        option.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        option.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void RightClickingABarOpensTheOverrideMenuNotTheKeyMenu()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        RightClickBar(0.75f);

        AnimatorKeyMenu menu = Session.GetComponent<AnimatorKeyMenu>();
        Assert.That(menu.OverrideOpen, Is.True);
        Assert.That(menu.Open, Is.False);
        Assert.That(UiLayout.IsHidden(Ui.OverrideMenu), Is.False);
    }

    [Test]
    public void RemoveDropsTheOverrideAndTheKeysItBaked()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        Assert.That(Shin.Keyframes, Is.Not.Empty);

        RightClickBar(0.75f);
        Choose(ToolActionKind.RemoveOverride);

        Assert.That(Idle.Targets, Is.Empty);
        Assert.That(Idle.Channels.Exists(c => c.TargetId == "shin" && c.Property == XafChannelProperty.Rotation &&
                                              c.Keyframes.Exists(k => k.Time >= 0.5f && k.Time <= 1f)), Is.False);
        Assert.That(Session.GetComponent<AnimatorKeyMenu>().OverrideOpen, Is.False);

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Idle.Targets, Has.Count.EqualTo(1));
    }

    [Test]
    public void KeepKeysDetachesTheOverrideAndLeavesItsKeys()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        int keys = Shin.Keyframes.Count;

        RightClickBar(0.75f);
        Choose(ToolActionKind.DetachOverride);

        Assert.That(Idle.Targets, Is.Empty);
        Assert.That(Shin.Keyframes, Has.Count.EqualTo(keys));
    }

    private XafChannel Shin => Idle.Channels.Find(c => c.TargetId == "shin" && c.Property == XafChannelProperty.Rotation)!;

    [TestCase(XafTargetShape.Spot)]
    [TestCase(XafTargetShape.Ellipse)]
    public void PinsAndEllipsesBothGetAnOverrideRowAboveTheKeys(XafTargetShape shape)
    {
        AddOverride(shape, 0.5f, 1f);
        Assert.That(Model().Tracks[0].IsOverride, Is.True);
        Assert.That(Model().Tracks[0].Id, Is.EqualTo("foot"));
    }

    [TestCase(XafTargetShape.Spot)]
    [TestCase(XafTargetShape.Ellipse)]
    public void DraggingTheEndChangesTheDuration(XafTargetShape shape)
    {
        XafLimbTarget target = AddOverride(shape, 0.5f, 1f);
        DragFromTo(1f, 1.5f);
        target = Idle.Targets[0];
        Assert.That(target.From, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(target.To, Is.EqualTo(1.5f).Within(0.01f));
    }

    [Test]
    public void DraggingTheStartChangesTheDuration()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        DragFromTo(0.5f, 0.2f);
        Assert.That(Idle.Targets[0].From, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(Idle.Targets[0].To, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void DraggingTheBodyMovesItAndKeepsItsDuration()
    {
        AddOverride(XafTargetShape.Ellipse, 0.5f, 1f);
        DragFromTo(0.75f, 1.25f);
        Assert.That(Idle.Targets[0].From, Is.EqualTo(1f).Within(0.01f));
        Assert.That(Idle.Targets[0].To - Idle.Targets[0].From, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void AnEndCannotPassTheOtherEndOrTheClipOrItsNeighbour()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        AddOverride(XafTargetShape.Spot, 1.5f, 1.8f);

        DragFromTo(1f, 1.9f);
        Assert.That(Idle.Targets[0].To, Is.EqualTo(1.5f).Within(0.001f));

        DragFromTo(0.5f, 1.7f);
        Assert.That(Idle.Targets[0].From, Is.LessThan(Idle.Targets[0].To));
    }

    [Test]
    public void ShorteningLeavesNoKeysOfTheOldPathBehind()
    {
        AddOverride(XafTargetShape.Ellipse, 0.5f, 1.5f);
        DragFromTo(1.5f, 1f);

        Assert.That(Shin.Keyframes.Exists(key => key.Time > 1.01f), Is.False);
        Assert.That(Shin.Keyframes.Exists(key => System.Math.Abs(key.Time - 1f) < 0.01f), Is.True);
    }

    [Test]
    public void TheWholeDragIsOneUndoStep()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        DragFromTo(1f, 1.5f);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(Package.Clips[0].Targets[0].To, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PressingABarSelectsItsBone()
    {
        AddOverride(XafTargetShape.Spot, 0.5f, 1f);
        DragFromTo(0.75f, 0.75f);
        Assert.That(Session.GetComponent<AnimatorSelection>().Bone, Is.EqualTo(Foot));
        Assert.That(Idle.Targets[0].From, Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void TheNewSpanIsSaved()
    {
        AddOverride(XafTargetShape.Ellipse, 0.5f, 1f);
        DragFromTo(1f, 1.5f);

        XafPackage saved = XafBinary.Read(XafBinary.Write(Package));

        Assert.That(saved.Clips[0].Targets[0].To, Is.EqualTo(1.5f).Within(0.01f));
    }
}
