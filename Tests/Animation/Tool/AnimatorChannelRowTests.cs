using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Systems.Groups;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Expanding a target row into per-channel rows and editing one channel.</summary>
[TestFixture]
public sealed class AnimatorChannelRowTests : AnimatorToolTest
{
    private static readonly RigTrack HeadTrack = new(RigChannelTarget.Bone, "head");

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new TimelineSystemGroup(Pointer, Pointer));
    }

    /// <summary>Head rotation at 0, 1, 2 plus separate scale channels at 0.5 and 1.5.</summary>
    protected override RigPackage CreatePackage()
    {
        RigPackage package = base.CreatePackage();
        RigClip clip = package.Clips[0];
        clip.Channels.Add(Channel(RigChannelProperty.ScaleX, 0.5f, 1.5f));
        clip.Channels.Add(Channel(RigChannelProperty.ScaleY, 0.5f, 1.5f));
        return package;
    }

    private static RigChannel Channel(RigChannelProperty property, params float[] times)
    {
        var channel = new RigChannel
        {
            Target = RigChannelTarget.Bone,
            TargetId = "head",
            Property = property
        };
        foreach (float time in times)
            channel.Keyframes.Add(new RigKeyframe { Time = time, Value = time });
        return channel;
    }

    private void Expand()
    {
        ref AnimatorTracks tracks = ref Session.GetComponent<AnimatorTracks>();
        tracks.Expanded ??= new HashSet<RigTrack>();
        tracks.Expanded.Add(HeadTrack);
        Frame();
    }

    private AnimatorTrackRow Row(RigChannelProperty? property)
    {
        foreach (AnimatorTrackRow row in Model().Tracks)
        {
            if (row.Track == HeadTrack && row.Property == property)
                return row;
        }

        Assert.Fail($"No head row for {property}");
        return default;
    }

    private Vector2 PointOn(AnimatorTrackRow row, float seconds)
    {
        AnimatorTimelineModel model = Model();
        int index = -1;
        for (int i = 0; i < model.Tracks.Count; i++)
        {
            if (model.Tracks[i] == row)
                index = i;
        }

        Assert.That(index, Is.GreaterThanOrEqualTo(model.FirstRow));
        Assert.That(index, Is.LessThan(model.FirstRow + model.VisibleRows));
        Rect2 rect = model.RowRect(index - model.FirstRow);
        return new Vector2(model.XForSeconds(seconds), rect.Position.Y + rect.Size.Y / 2f);
    }

    private void Press(Vector2 position, bool secondary = false)
    {
        Pointer.Position = position;
        Pointer.Pressed = !secondary;
        Pointer.Secondary = secondary;
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
        Pointer.Secondary = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    private void ExpandByArrow()
    {
        AnimatorTimelineModel model = Model();
        int index = -1;
        for (int i = 0; i < model.Tracks.Count; i++)
        {
            if (model.Tracks[i].Track == HeadTrack && !model.Tracks[i].IsChannel)
                index = i;
        }

        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        float y = model.RowRect(index - model.FirstRow).Position.Y + AnimatorTimelineModel.RowHeight / 2f;
        Press(new Vector2(model.Labels.Position.X + 8, y));
        Release();
    }

    [Test]
    public void ClickingTheArrowExpandsAndCollapsesTheChannelRows()
    {
        ExpandByArrow();

        Assert.That(Row(RigChannelProperty.Rotation).IsChannel, Is.True);
        Assert.That(Row(RigChannelProperty.ScaleX).IsChannel, Is.True);
        Assert.That(Model().Tracks, Has.Count.EqualTo(4));

        ExpandByArrow();

        Assert.That(Model().Tracks, Has.Count.EqualTo(1));
    }

    [Test]
    public void AChannelRowOnlyShowsItsOwnKeyTimes()
    {
        Expand();

        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.Rotation)),
            Is.EqualTo(new[] { 0f, 1f, 2f }));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.ScaleX)), Is.EqualTo(new[] { 0.5f, 1.5f }));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(null)), Is.EqualTo(new[] { 0f, 0.5f, 1f, 1.5f, 2f }));
    }

    [Test]
    public void DraggingAChannelKeyRetimesOnlyThatChannel()
    {
        Expand();
        RigChannel scaleX = ChannelOf(RigChannelProperty.ScaleX);

        Press(PointOn(Row(RigChannelProperty.ScaleX), 0.5f));
        MoveTo(new Vector2(Model().XForSeconds(0.75f), Pointer.Position.Y));
        Release();

        Assert.That(scaleX.Keyframes[0].Time, Is.EqualTo(0.75f).Within(0.03f));
        Assert.That(ChannelOf(RigChannelProperty.Rotation).Keyframes[1].Time, Is.EqualTo(1f));
    }

    [Test]
    public void DeletingAChannelKeyLeavesTheOtherChannels()
    {
        Expand();

        Press(PointOn(Row(RigChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyCommands.DeleteSelected(Session);

        Assert.That(ChannelOf(RigChannelProperty.ScaleX).Keyframes, Has.Count.EqualTo(1));
        Assert.That(ChannelOf(RigChannelProperty.Rotation).Keyframes, Has.Count.EqualTo(3));
        Assert.That(ChannelOf(RigChannelProperty.ScaleY).Keyframes, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddingAKeyOnAChannelRowOnlyAddsThatChannel()
    {
        Expand();

        AnimatorKeyCommands.AddKeyAt(Session, Row(RigChannelProperty.ScaleY), 0.25f);

        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.ScaleY)), Does.Contain(0.25f));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.Rotation)), Does.Not.Contain(0.25f));
    }

    [Test]
    public void EasingAChannelKeyOnlyChangesThatChannel()
    {
        Expand();

        Press(PointOn(Row(RigChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyCommands.SetInterpolation(Session, EasePreset.Step);

        Assert.That(
            RigEasing.Read(Idle, RigChannelTarget.Bone, "head", RigChannelProperty.ScaleX, 0.5f),
            Is.EqualTo(EasePreset.Step));
        Assert.That(
            RigEasing.Read(Idle, RigChannelTarget.Bone, "head", RigChannelProperty.Rotation, 1f),
            Is.EqualTo(EasePreset.Linear));
    }

    [Test]
    public void CopyPasteOfAChannelKeyOnlyPastesThatChannel()
    {
        Expand();
        Press(PointOn(Row(RigChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(0.25f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.ScaleX)), Does.Contain(0.25f));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.Rotation)), Does.Not.Contain(0.25f));
        Assert.That(Session.GetComponent<AnimatorSelection>().KeyProperty, Is.EqualTo(RigChannelProperty.ScaleX));
    }

    [Test]
    public void TheTargetRowStillKeysEveryChannel()
    {
        Expand();
        AnimatorKeyCommands.AddKeyAt(Session, Row(null), 0.25f);

        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.Rotation)), Does.Contain(0.25f));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.ScaleX)), Does.Contain(0.25f));
        Assert.That(AnimatorTrackRows.KeyTimes(Idle, Row(RigChannelProperty.ScaleY)), Does.Contain(0.25f));
    }

    [Test]
    public void SelectAllInChannelModeSelectsTheVisibleChannelKeys()
    {
        Expand();

        AnimatorKeyClipboardCommands.SelectAll(Session);

        List<AnimatorKeyRef> keys = Session.GetComponent<AnimatorSelection>().Keys();
        Assert.That(keys.Exists(key => key.Property == RigChannelProperty.ScaleX), Is.True);
        Assert.That(keys.Exists(key => key.Property == RigChannelProperty.Rotation), Is.True);
        Assert.That(keys.Exists(key => key.Property is null), Is.True);
    }

    private RigChannel ChannelOf(RigChannelProperty property) =>
        Idle.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == "head" &&
                                      channel.Property == property)!;
}
