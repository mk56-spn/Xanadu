using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorTimelineInputTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private Vector2 OnRuler(float seconds) => new(
        Model().XForSeconds(seconds),
        Model().Ruler.Position.Y + 8);

    private void PressAt(Vector2 position)
    {
        Pointer.Position = position;
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
    }

    private void Release()
    {
        Pointer.Pressed = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    private List<float> HeadTimes() =>
        XafTracks.KeyTimes(Idle, new XafTrack(XafChannelTarget.Bone, "head"));

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    [Test]
    public void PressingAKeyDiamondSelectsItsKeySetAndSeeksToIt()
    {
        Runtime.SetPaused(false);

        PressAt(KeyPoint("head", 1f));

        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyId, Is.EqualTo("head"));
        Assert.That(Selection.KeyTime, Is.EqualTo(1f));
        Assert.That(Selection.Bone, Is.EqualTo(2));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(1f).Within(0.001f));
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void DraggingAKeySetRetimesEveryChannelAndSnaps()
    {
        XafAuthoring.KeyBoneTransform(Package, Idle, 2, 1f, XafTransform.Identity);
        PressAt(KeyPoint("head", 1f));

        Pointer.Position = KeyPoint("head", 1.504f);
        Frame();

        Assert.That(HeadTimes(), Is.EqualTo(new[] { 0f, 1.5f, 2f }).Within(0.0001f));
        foreach (XafChannel channel in Idle.Channels)
            Assert.That(channel.Keyframes.Exists(key => System.Math.Abs(key.Time - 1.5f) < 0.001f), Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(1.5f).Within(0.001f));
        Release();
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    [Test]
    public void DraggingAKeyAcrossAnotherAndBackLeavesBothWhereTheyWere()
    {
        PressAt(KeyPoint("head", 1f));

        foreach (float time in new[] { 1.5f, 2f, 1.5f, 1f })
        {
            Pointer.Position = KeyPoint("head", time);
            Frame();
        }

        Assert.That(HeadTimes(), Is.EqualTo(new[] { 0f, 1f, 2f }).Within(0.0001f));
    }

    [Test]
    public void DroppingAKeyOnAnotherReplacesOnlyThatOne()
    {
        PressAt(KeyPoint("head", 1f));

        foreach (float time in new[] { 1.5f, 2f })
        {
            Pointer.Position = KeyPoint("head", time);
            Frame();
        }
        Release();

        Assert.That(HeadTimes(), Is.EqualTo(new[] { 0f, 2f }).Within(0.0001f));
    }

    [Test]
    public void CrossingAnotherChannelsKeyDoesNotPickItUp()
    {
        var translation = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "head",
            Property = XafChannelProperty.TranslationX,
        };
        translation.Keyframes.Add(new XafKeyframe { Time = 1.5f, Value = 4f });
        Idle.Channels.Add(translation);
        PressAt(KeyPoint("head", 1f));
        Pointer.Shift = true;

        foreach (float time in new[] { 1.25f, 1.5f, 1.75f })
        {
            Pointer.Position = KeyPoint("head", time);
            Frame();
        }
        Release();

        Assert.That(translation.Keyframes, Has.Count.EqualTo(1));
        Assert.That(translation.Keyframes[0].Time, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(Selection.Keys(), Has.Count.EqualTo(1));
        Assert.That(Selection.KeyTime, Is.EqualTo(1.75f).Within(0.002f));
    }

    [Test]
    public void HoldingShiftRetimesWithoutSnapping()
    {
        PressAt(KeyPoint("head", 1f));
        Pointer.Shift = true;

        Pointer.Position = KeyPoint("head", 1.51f);
        Frame();

        Assert.That(Selection.KeyTime, Is.EqualTo(1.51f).Within(0.002f));
    }

    [Test]
    public void ScrubbingOnTheRulerSeeksAndSnapsAndClearsTheKeySelection()
    {
        PressAt(KeyPoint("head", 1f));
        Release();

        PressAt(OnRuler(0.73f));

        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.74f).Within(0.001f));
        Assert.That(Selection.HasKey, Is.False);
        Assert.That(Runtime.Paused, Is.True);
    }

    [Test]
    public void DraggingAfterAScrubKeepsScrubbing()
    {
        PressAt(OnRuler(0.5f));

        Pointer.Position = OnRuler(1.75f);
        Frame();

        Assert.That(Runtime.CurrentTime, Is.EqualTo(1.76f).Within(0.021f));
        Release();
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
        Assert.That(Session.GetComponent<TimelineView>().Scrubbing, Is.False);
    }

    [Test]
    public void TheVisibleWindowStaysStillWhileDraggingWhenZoomedIn()
    {
        Session.GetComponent<TimelineView>().Zoom = 4f;
        Runtime.Seek(1f);
        Frame();
        TimeWindowSnapshot before = Snapshot();
        PressAt(Model().Ruler.Position + new Vector2(Model().Ruler.Size.X / 2, 8));

        Pointer.Position += new Vector2(300, 0);
        Frame(3);

        Assert.That(Session.GetComponent<TimelineView>().Scrubbing, Is.True);
        Assert.That(Snapshot().Start, Is.EqualTo(before.Start));
        Release();
        Assert.That(Session.GetComponent<TimelineView>().Scrubbing, Is.False);
    }

    [Test]
    public void ClickingEmptySpaceOnARowSelectsItsBoneAndScrubs()
    {
        PressAt(KeyPoint("head", 0.5f));

        Assert.That(Selection.Bone, Is.EqualTo(2));
        Assert.That(Selection.HasKey, Is.False);
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.5f).Within(0.02f));
    }

    [Test]
    public void ClickingATrackLabelSelectsItsBoneWithoutMovingThePlayhead()
    {
        Runtime.Seek(0.4f);
        AnimatorTimelineModel model = Model();
        Vector2 label = new(model.Labels.Position.X + 20, model.RowRect(0).Position.Y + 8);

        PressAt(label);

        Assert.That(Selection.Bone, Is.EqualTo(2));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
    }

    [Test]
    public void TheSelectedBoneAlwaysHasARowEvenWithoutKeys()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 1;

        Assert.That(
            Model().Tracks,
            Has.Some.Matches<AnimatorTrackRow>(row =>
                row.Track == new XafTrack(XafChannelTarget.Bone, "torso") &&
                !row.IsChannel));
    }

    [Test]
    public void DeleteRemovesTheSelectedKeySet()
    {
        PressAt(KeyPoint("head", 1f));
        Release();

        Keyboard.Press(Key.Delete);
        Frame(3);

        Assert.That(HeadTimes(), Is.EqualTo(new[] { 0f, 2f }));
        Assert.That(Selection.HasKey, Is.False);
    }

    [Test]
    public void BackspaceRemovesTheSelectedKeySet()
    {
        PressAt(KeyPoint("head", 1f));
        Release();

        Keyboard.Press(Key.Backspace);
        Frame(3);

        Assert.That(HeadTimes(), Is.EqualTo(new[] { 0f, 2f }));
    }

    [Test]
    public void DeleteWithNothingSelectedDoesNothing()
    {
        Keyboard.Press(Key.Delete);
        Frame();

        Assert.That(HeadTimes(), Has.Count.EqualTo(3));
    }

    [Test]
    public void PressesInTheHeaderAreIgnored()
    {
        AnimatorTimelineModel model = Model();

        PressAt(new Vector2(model.Ruler.Position.X + 100, model.Panel.Position.Y + 14));

        Assert.That(Session.GetComponent<AnimatorDrag>().Kind, Is.EqualTo(AnimatorDragKind.None));
        Assert.That(Selection.Bone, Is.EqualTo(-1));
    }

    [Test]
    public void ControlWheelStepsThroughTheSharedZoomLevels()
    {
        Pointer.Position = Model().Rows.Position + new Vector2(100, 5);
        Pointer.Control = true;

        Pointer.Wheel = 1;
        Frame();
        Assert.That(Session.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));

        Pointer.Wheel = 1;
        Frame();
        Assert.That(Session.GetComponent<TimelineView>().Zoom, Is.EqualTo(4f));

        Pointer.Wheel = -1;
        Frame();
        Assert.That(Session.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));
    }

    [Test]
    public void WheelOverTheRulerZoomsWithoutControl()
    {
        Pointer.Position = OnRuler(1f);
        Pointer.Control = false;

        Pointer.Wheel = 1;
        Frame();

        Assert.That(Session.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));
    }

    [Test]
    public void WheelOverTheRowsScrollsTracksAndClamps()
    {
        for (int i = 0; i < 12; i++)
        {
            string id = $"extra-{i}";
            XafRigAuthoring.AddBone(Package, 0, id, Vector2.Zero);
            XafAuthoring.SetBoneProperty(Package, Idle, id, XafChannelProperty.Rotation, 0.5f, 1f);
        }

        AnimatorTimelineModel model = Model();
        int max = model.Tracks.Count - model.VisibleRows;
        Assert.That(max, Is.GreaterThan(0));
        Pointer.Position = model.Rows.Position + new Vector2(100, 5);
        Pointer.Control = false;

        Pointer.Wheel = -1;
        Frame();
        Assert.That(Session.GetComponent<AnimatorTracks>().FirstRow, Is.EqualTo(1));
        Assert.That(Model().FirstRow, Is.EqualTo(1));

        Pointer.Wheel = 1;
        Frame();
        Pointer.Wheel = 1;
        Frame();
        Assert.That(Session.GetComponent<AnimatorTracks>().FirstRow, Is.EqualTo(0));

        for (int i = 0; i < 40; i++)
        {
            Pointer.Wheel = -1;
            Frame();
        }
        Assert.That(Session.GetComponent<AnimatorTracks>().FirstRow, Is.EqualTo(max));
    }

    [Test]
    public void WheelOutsideThePanelDoesNothing()
    {
        Pointer.Position = new Vector2(400, 300);
        Pointer.Control = true;

        Pointer.Wheel = 1;
        Frame();

        Assert.That(Session.GetComponent<TimelineView>().Zoom, Is.EqualTo(1f));
    }

    [Test]
    public void ZoomedInTheWindowFollowsThePlayhead()
    {
        Session.GetComponent<TimelineView>().Zoom = 4f;
        Runtime.Seek(0f);
        long startAtZero = Model().Window.Start;

        Runtime.Seek(1.8f);

        Assert.That(startAtZero, Is.EqualTo(0));
        Assert.That(Model().Window.Start, Is.GreaterThan(1000));
    }

    private readonly record struct TimeWindowSnapshot(long Start, long End);

    private TimeWindowSnapshot Snapshot() => new(Model().Window.Start, Model().Window.End);
}
