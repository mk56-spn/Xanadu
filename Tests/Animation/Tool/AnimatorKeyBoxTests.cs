using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

/// <summary>Dragging a box over empty timeline row space selects the keys it covers.</summary>
[TestFixture]
public sealed class AnimatorKeyBoxTests : AnimatorToolTest
{
    private static readonly XafTrack RootTrack = new(XafChannelTarget.Bone, "root");
    private static readonly XafTrack TorsoTrack = new(XafChannelTarget.Bone, "torso");
    private static readonly XafTrack HeadTrack = new(XafChannelTarget.Bone, "head");

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorKeyMenuSystem(Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    /// <summary>Rows, top to bottom: root (keys 0, 2), torso (0.5, 1.5), head (0, 1, 2).</summary>
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        package.Clips[0].Channels.Add(Channel("root", 0f, 2f));
        package.Clips[0].Channels.Add(Channel("torso", 0.5f, 1.5f));
        return package;
    }

    private static XafChannel Channel(string bone, params float[] times)
    {
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = bone,
            Property = XafChannelProperty.Rotation,
        };
        foreach (float time in times)
            channel.Keyframes.Add(new XafKeyframe { Time = time, Value = time });
        return channel;
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private List<AnimatorKeyRef> Selected => Selection.Keys();

    /// <summary>A screen point on a track's row at a time; empty space unless a key is there.</summary>
    private Vector2 At(XafTrack track, float seconds) =>
        new(Model().XForSeconds(seconds), KeyPoint(track.Id, 0f).Y);

    private static AnimatorKeyRef Ref(XafTrack track, float time) => new(track.Target, track.Id, time);

    private void Press(Vector2 position, bool shift = false)
    {
        Pointer.Position = position;
        Pointer.Shift = shift;
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
    }

    private void MoveTo(Vector2 position)
    {
        Pointer.Position = position;
        Frame();
    }

    private void Release()
    {
        Pointer.Pressed = false;
        Pointer.Shift = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    private void DragBox(Vector2 from, Vector2 to, bool shift = false)
    {
        Press(from, shift);
        MoveTo(to);
        Release();
    }

    private List<float> Times(XafTrack track) => XafTracks.KeyTimes(Idle, track);

    /// <summary>Box over the torso key at 0.5 and the head key at 1.</summary>
    private void SelectTorsoAndHead() =>
        DragBox(At(RootTrack, 0.25f), At(HeadTrack, 1.2f));

    [Test]
    public void DraggingABoxSelectsEveryKeyItCovers()
    {
        // From empty space on the root row, down to the head row.
        DragBox(At(RootTrack, 0.25f), At(HeadTrack, 1.2f));

        Assert.That(Selected, Is.EquivalentTo(new[] { Ref(TorsoTrack, 0.5f), Ref(HeadTrack, 1f) }));
        Assert.That(Selection.HasKey, Is.True);
    }

    [Test]
    public void TheBoxWorksWhicheverWayItIsDragged()
    {
        DragBox(At(HeadTrack, 1.2f), At(RootTrack, 0.25f));

        Assert.That(Selected, Is.EquivalentTo(new[] { Ref(TorsoTrack, 0.5f), Ref(HeadTrack, 1f) }));
    }

    [Test]
    public void KeysOutsideTheBoxStayUnselected()
    {
        DragBox(At(TorsoTrack, 1.2f), At(TorsoTrack, 1.8f));

        Assert.That(Selected, Is.EqualTo(new[] { Ref(TorsoTrack, 1.5f) }));
    }

    [Test]
    public void ABoxThatCoversNothingClearsTheOldSelection()
    {
        SelectTorsoAndHead();

        DragBox(At(RootTrack, 0.8f), At(RootTrack, 1.4f));

        Assert.That(Selection.HasKey, Is.False);
        Assert.That(Selected, Is.Empty);
    }

    [Test]
    public void APlainBoxReplacesThePreviousSelection()
    {
        SelectTorsoAndHead();

        DragBox(At(TorsoTrack, 1.2f), At(TorsoTrack, 1.8f));

        Assert.That(Selected, Is.EqualTo(new[] { Ref(TorsoTrack, 1.5f) }));
    }

    [Test]
    public void AShiftBoxAddsToTheSelection()
    {
        SelectTorsoAndHead();

        DragBox(At(TorsoTrack, 1.2f), At(TorsoTrack, 1.8f), shift: true);

        Assert.That(Selected, Is.EquivalentTo(new[] { Ref(TorsoTrack, 0.5f), Ref(HeadTrack, 1f), Ref(TorsoTrack, 1.5f) }));
    }

    [Test]
    public void AShiftBoxThatCoversAnAlreadySelectedKeyDoesNotDuplicateIt()
    {
        SelectTorsoAndHead();

        DragBox(At(RootTrack, 0.25f), At(HeadTrack, 1.2f), shift: true);

        Assert.That(Selected, Has.Count.EqualTo(2));
    }

    [Test]
    public void TheBoxIsPublishedWhileDraggingAndClearedOnRelease()
    {
        Vector2 from = At(RootTrack, 0.25f);
        Vector2 to = At(HeadTrack, 1.2f);

        Press(from);
        MoveTo(to);

        UiMarqueeBox box = Session.GetComponent<UiMarqueeBox>();
        Assert.That(box.Active, Is.True);
        Assert.That(box.Bounds, Is.EqualTo(UiMarqueeBox.Between(from, to)));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.KeyBox));

        Release();

        Assert.That(Session.GetComponent<UiMarqueeBox>().Active, Is.False);
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
        Assert.That(Session.GetComponent<TimelineView>().Scrubbing, Is.False);
    }

    [Test]
    public void APressThatNeverBecomesABoxIsStillAClickOnTheRow()
    {
        Runtime.SetPaused(false);
        Vector2 click = At(TorsoTrack, 0.9f);
        Press(click);

        // A tiny wobble stays under the dead zone.
        MoveTo(click + new Vector2(2, 1));
        Release();

        Assert.That(Selection.Bone, Is.EqualTo(1));
        Assert.That(Selection.HasKey, Is.False);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.9f).Within(0.02f));
        Assert.That(Runtime.Paused, Is.True);
        Assert.That(Session.GetComponent<UiMarqueeBox>().Active, Is.False);
    }

    [Test]
    public void ClickingEmptySpaceClearsTheSelectedKeys()
    {
        SelectTorsoAndHead();

        Press(At(RootTrack, 1.2f));
        Release();

        Assert.That(Selection.HasKey, Is.False);
    }

    [Test]
    public void TheVisibleWindowStaysStillWhileBoxing()
    {
        Session.GetComponent<TimelineView>().Zoom = 4f;
        Runtime.Seek(1f);
        Frame();
        long before = Model().Window.Start;

        Press(At(RootTrack, 0.9f));
        MoveTo(At(HeadTrack, 1.2f));
        Frame(3);

        Assert.That(Session.GetComponent<TimelineView>().Scrubbing, Is.True);
        Assert.That(Model().Window.Start, Is.EqualTo(before));
        Release();
    }

    [Test]
    public void DeleteRemovesEverySelectedKey()
    {
        SelectTorsoAndHead();

        Keyboard.Press(Godot.Key.Delete);
        Frame(3);

        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 1.5f }));
        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 2f }));
        Assert.That(Times(RootTrack), Is.EqualTo(new[] { 0f, 2f }));
        Assert.That(Selection.HasKey, Is.False);
        Assert.That(Selection.Extra, Is.Null);
    }

    [Test]
    public void DeletingAGroupIsOneUndoStep()
    {
        SelectTorsoAndHead();
        Keyboard.Press(Godot.Key.Delete);
        Frame(3);
        Keyboard.Clear();
        Frame();

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 0.5f, 1.5f }));
        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 1f, 2f }));
    }

    [Test]
    public void DraggingASelectedKeyMovesTheWholeGroupTogether()
    {
        SelectTorsoAndHead();

        // Grab the head key at 1 and pull it half a second later.
        Press(KeyPoint("head", 1f));
        MoveTo(At(HeadTrack, 1.5f));
        Release();

        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 1.5f, 2f }).Within(0.001f));
        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 1f, 1.5f }).Within(0.001f));
        Assert.That(Selected.Select(key => key.Time).OrderBy(time => time),
            Is.EqualTo(new[] { 1f, 1.5f }).Within(0.001f));
    }

    [Test]
    public void TheKeyThatWasGrabbedAnchorsTheDrag()
    {
        SelectTorsoAndHead();

        // The torso key is not the primary one, but it is the one pressed.
        Press(KeyPoint("torso", 0.5f));
        Assert.That(Selection.KeyId, Is.EqualTo("torso"));
        MoveTo(At(TorsoTrack, 0.7f));
        Release();

        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 0.7f, 1.5f }).Within(0.001f));
        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 1.2f, 2f }).Within(0.001f));
    }

    [Test]
    public void TheGroupStopsWhereItsLastKeyReachesTheClipEnd()
    {
        SelectTorsoAndHead();

        // Dragged well past the end: the head key can only travel 1 s (2 - 1) and
        // the torso key goes the same 1 s, each replacing the key already there.
        Press(KeyPoint("head", 1f));
        MoveTo(At(HeadTrack, 2.5f));
        Release();

        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 2f }).Within(0.001f));
        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 1.5f }).Within(0.001f));
        Assert.That(Selected.Select(key => key.Time).OrderBy(time => time),
            Is.EqualTo(new[] { 1.5f, 2f }).Within(0.001f));
    }

    [Test]
    public void MovingAGroupIsOneUndoStep()
    {
        SelectTorsoAndHead();
        Press(KeyPoint("head", 1f));
        MoveTo(At(HeadTrack, 1.3f));
        MoveTo(At(HeadTrack, 1.5f));
        Release();

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 1f, 2f }).Within(0.001f));
        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 0.5f, 1.5f }).Within(0.001f));
    }

    [Test]
    public void PressingAKeyOutsideTheGroupReplacesTheSelection()
    {
        SelectTorsoAndHead();

        Press(KeyPoint("torso", 1.5f));
        Release();

        Assert.That(Selected, Is.EqualTo(new[] { Ref(TorsoTrack, 1.5f) }));
    }

    [Test]
    public void EasingPresetsApplyToEverySelectedKey()
    {
        SelectTorsoAndHead();

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Interpolate, XafEasePreset.Step.ToString()));

        Assert.That(XafEasing.Read(Idle, XafChannelTarget.Bone, "torso", 0.5f), Is.EqualTo(XafEasePreset.Step));
        Assert.That(XafEasing.Read(Idle, XafChannelTarget.Bone, "head", 1f), Is.EqualTo(XafEasePreset.Step));
        Assert.That(XafEasing.Read(Idle, XafChannelTarget.Bone, "torso", 1.5f), Is.Not.EqualTo(XafEasePreset.Step));
    }

    [Test]
    public void RightClickingAGroupedKeyKeepsTheGroupAndDeleteKeyRemovesIt()
    {
        SelectTorsoAndHead();

        Pointer.Position = KeyPoint("torso", 0.5f);
        Pointer.Secondary = true;
        Frame();
        Pointer.Secondary = false;
        Frame();
        Assert.That(Selected, Has.Count.EqualTo(2));

        Pointer.Pressed = true;
        Ui.KeyMenuDelete.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        Ui.KeyMenuDelete.RemoveTag<Pressed>();
        Frame();

        Assert.That(Times(TorsoTrack), Is.EqualTo(new[] { 1.5f }));
        Assert.That(Times(HeadTrack), Is.EqualTo(new[] { 0f, 2f }));
    }

    [Test]
    public void ClearingTheKeySelectionDropsTheGroupToo()
    {
        SelectTorsoAndHead();
        Assert.That(Selection.Extra, Is.Not.Null);

        Selection_Clear();

        Assert.That(Selection.Extra, Is.Null);
        Assert.That(Selection.Keys(), Is.Empty);
    }

    private void Selection_Clear() => Session.GetComponent<AnimatorSelection>().HasKey = false;

    [Test]
    public void ModelFindsKeysByTheirMarkersNotJustTheirCentres()
    {
        AnimatorTimelineModel model = Model();
        Rect2 rows = model.Rows;

        // A box over every row and the whole width finds every key set.
        Assert.That(model.KeysIn(rows), Has.Count.EqualTo(7));

        // A zero-width box exactly on a key still touches it.
        Vector2 key = KeyPoint("head", 1f);
        Assert.That(model.KeysIn(new Rect2(key.X, rows.Position.Y, 0, rows.Size.Y)),
            Is.EqualTo(new[] { Ref(HeadTrack, 1f) }));

        // A box between two rows touches neither row's keys when it misses their markers.
        Assert.That(model.KeysIn(new Rect2(rows.Position.X, rows.Position.Y, 4, 4)).Select(k => k.Time),
            Is.EqualTo(new[] { 0f }));
    }
}
