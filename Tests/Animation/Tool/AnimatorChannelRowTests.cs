using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Expanding a target row into per-channel rows and editing one channel.</summary>
[TestFixture]
public sealed class AnimatorChannelRowTests : AnimatorToolTest
{
    private static readonly XafTrack HeadTrack = new(XafChannelTarget.Bone, "head");

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
    }

    /// <summary>Head rotation at 0, 1, 2 plus separate scale channels at 0.5 and 1.5.</summary>
    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        XafClip clip = package.Clips[0];
        clip.Channels.Add(Channel(XafChannelProperty.ScaleX, 0.5f, 1.5f));
        clip.Channels.Add(Channel(XafChannelProperty.ScaleY, 0.5f, 1.5f));
        return package;
    }

    private static XafChannel Channel(XafChannelProperty property, params float[] times)
    {
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "head",
            Property = property,
        };
        foreach (float time in times)
            channel.Keyframes.Add(new XafKeyframe { Time = time, Value = time });
        return channel;
    }

    private void Expand()
    {
        ref AnimatorTracks tracks = ref Session.GetComponent<AnimatorTracks>();
        tracks.Expanded ??= new HashSet<XafTrack>();
        tracks.Expanded.Add(HeadTrack);
        Frame();
    }

    private AnimatorTrackRow Row(XafChannelProperty? property)
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

        Assert.That(Row(XafChannelProperty.Rotation).IsChannel, Is.True);
        Assert.That(Row(XafChannelProperty.ScaleX).IsChannel, Is.True);
        Assert.That(Model().Tracks, Has.Count.EqualTo(4));

        ExpandByArrow();

        Assert.That(Model().Tracks, Has.Count.EqualTo(1));
    }

    [Test]
    public void AChannelRowOnlyShowsItsOwnKeyTimes()
    {
        Expand();

        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.Rotation)),
            Is.EqualTo(new[] { 0f, 1f, 2f }));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.ScaleX)),
            Is.EqualTo(new[] { 0.5f, 1.5f }));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(null)),
            Is.EqualTo(new[] { 0f, 0.5f, 1f, 1.5f, 2f }));
    }

    [Test]
    public void DraggingAChannelKeyRetimesOnlyThatChannel()
    {
        Expand();
        XafChannel scaleX = ChannelOf(XafChannelProperty.ScaleX);

        Press(PointOn(Row(XafChannelProperty.ScaleX), 0.5f));
        MoveTo(new Vector2(Model().XForSeconds(0.75f), Pointer.Position.Y));
        Release();

        Assert.That(scaleX.Keyframes[0].Time, Is.EqualTo(0.75f).Within(0.03f));
        Assert.That(ChannelOf(XafChannelProperty.Rotation).Keyframes[1].Time, Is.EqualTo(1f));
    }

    [Test]
    public void DeletingAChannelKeyLeavesTheOtherChannels()
    {
        Expand();

        Press(PointOn(Row(XafChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyCommands.DeleteSelected(Session);

        Assert.That(ChannelOf(XafChannelProperty.ScaleX).Keyframes, Has.Count.EqualTo(1));
        Assert.That(ChannelOf(XafChannelProperty.Rotation).Keyframes, Has.Count.EqualTo(3));
        Assert.That(ChannelOf(XafChannelProperty.ScaleY).Keyframes, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddingAKeyOnAChannelRowOnlyAddsThatChannel()
    {
        Expand();

        AnimatorKeyCommands.AddKeyAt(Session, Row(XafChannelProperty.ScaleY), 0.25f);

        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.ScaleY)),
            Does.Contain(0.25f));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.Rotation)),
            Does.Not.Contain(0.25f));
    }

    [Test]
    public void EasingAChannelKeyOnlyChangesThatChannel()
    {
        Expand();

        Press(PointOn(Row(XafChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyCommands.SetInterpolation(Session, XafEasePreset.Step);

        Assert.That(
            XafEasing.Read(Idle, XafChannelTarget.Bone, "head", XafChannelProperty.ScaleX, 0.5f),
            Is.EqualTo(XafEasePreset.Step));
        Assert.That(
            XafEasing.Read(Idle, XafChannelTarget.Bone, "head", XafChannelProperty.Rotation, 1f),
            Is.EqualTo(XafEasePreset.Linear));
    }

    [Test]
    public void CopyPasteOfAChannelKeyOnlyPastesThatChannel()
    {
        Expand();
        Press(PointOn(Row(XafChannelProperty.ScaleX), 0.5f));
        Release();
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(0.25f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.ScaleX)),
            Does.Contain(0.25f));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.Rotation)),
            Does.Not.Contain(0.25f));
        Assert.That(Session.GetComponent<AnimatorSelection>().KeyProperty,
            Is.EqualTo(XafChannelProperty.ScaleX));
    }

    [Test]
    public void TheTargetRowStillKeysEveryChannel()
    {
        Expand();
        AnimatorKeyCommands.AddKeyAt(Session, Row(null), 0.25f);

        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.Rotation)),
            Does.Contain(0.25f));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.ScaleX)),
            Does.Contain(0.25f));
        Assert.That(
            AnimatorTrackRows.KeyTimes(Idle, Row(XafChannelProperty.ScaleY)),
            Does.Contain(0.25f));
    }

    [Test]
    public void SelectAllInChannelModeSelectsTheVisibleChannelKeys()
    {
        Expand();

        AnimatorKeyClipboardCommands.SelectAll(Session);

        List<AnimatorKeyRef> keys = Session.GetComponent<AnimatorSelection>().Keys();
        Assert.That(keys.Exists(key => key.Property == XafChannelProperty.ScaleX), Is.True);
        Assert.That(keys.Exists(key => key.Property == XafChannelProperty.Rotation), Is.True);
        Assert.That(keys.Exists(key => key.Property is null), Is.True);
    }

    private XafChannel ChannelOf(XafChannelProperty property) =>
        Idle.Channels.Find(channel =>
            channel.Target == XafChannelTarget.Bone &&
            channel.TargetId == "head" &&
            channel.Property == property)!;
}
