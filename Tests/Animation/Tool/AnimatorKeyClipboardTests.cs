using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Ui.Systems.Groups;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Copying, pasting and bulk-selecting key sets.</summary>
[TestFixture]
public sealed class AnimatorKeyClipboardTests : AnimatorToolTest
{
    private static readonly RigTrack HeadTrack = new(RigChannelTarget.Bone, "head");

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new TimelineSystemGroup(Pointer, Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private static RigChannel HeadChannel(RigClip clip) =>
        clip.Channels.Find(channel => channel.Target == RigChannelTarget.Bone && channel.TargetId == "head" &&
                                      channel.Property == RigChannelProperty.Rotation)!;

    private static RigKeyframe? KeyAt(RigClip clip, float time) =>
        HeadChannel(clip).Keyframes.Find(key => System.Math.Abs(key.Time - time) <= RigTracks.TimeTolerance);

    private void Press(Vector2 position, bool shift = false)
    {
        Pointer.Position = position;
        Pointer.Shift = shift;
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
    }

    private void Release()
    {
        Pointer.Pressed = false;
        Pointer.Shift = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void SelectedKeysCanBePastedAtThePlayhead()
    {
        AnimatorKeyCommands.SelectKey(Session, HeadTrack, 1f);
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(0.25f);
        AnimatorKeyClipboardCommands.Paste(Session);

        RigKeyframe? key = KeyAt(Idle, 0.25f);
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.Value, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void PasteKeepsTheCurveOfTheCopiedKey()
    {
        RigEasing.Apply(Idle, RigChannelTarget.Bone, "head", 1f, EasePreset.Step);
        AnimatorKeyCommands.SelectKey(Session, HeadTrack, 1f);
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(0.5f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(KeyAt(Idle, 0.5f)!.Interpolation, Is.EqualTo(RigInterpolation.Step));
    }

    [Test]
    public void KeysCanBePastedIntoAnotherClip()
    {
        AnimatorKeyCommands.SelectKey(Session, HeadTrack, 1f);
        AnimatorKeyClipboardCommands.Copy(Session);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Clip, "jump"));

        AnimatorKeyClipboardCommands.Paste(Session);

        RigClip jump = Package.Clips[1];
        Assert.That(jump.Channels, Has.Count.EqualTo(1));
        Assert.That(KeyAt(jump, 0f), Is.Not.Null);
    }

    [Test]
    public void PasteMovesTheBlockBackSoItFitsTheClip()
    {
        AnimatorKeyCommands.SelectKeys(
            Session,
            new List<AnimatorKeyRef>
            {
                new(RigChannelTarget.Bone, "head", 1f),
                new(RigChannelTarget.Bone, "head", 2f)
            });
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(2f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("AT 1.00s"));
        Assert.That(KeyAt(Idle, 1f), Is.Not.Null);
        Assert.That(KeyAt(Idle, 2f), Is.Not.Null);
        Assert.That(HeadChannel(Idle).Keyframes.Exists(key => key.Time > Idle.Duration + 0.0001f), Is.False);
    }

    [Test]
    public void SelectAllTakesEveryKeySetInTheClip()
    {
        AnimatorKeyClipboardCommands.SelectAll(Session);

        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.Keys(), Has.Count.EqualTo(3));
    }

    [Test]
    public void ShiftClickingAKeyAddsItAndThenTakesItOutAgain()
    {
        Press(KeyPoint("head", 1f), true);
        Assert.That(Selection.Keys(), Has.Count.EqualTo(1));
        Release();

        Press(KeyPoint("head", 0f), true);
        Assert.That(Selection.Keys(), Has.Count.EqualTo(2));
        Release();

        Press(KeyPoint("head", 1f), true);
        Release();
        List<AnimatorKeyRef> keys = Selection.Keys();
        Assert.That(keys, Has.Count.EqualTo(1));
        Assert.That(keys[0].Time, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void CopyingWithoutASelectionSaysSo()
    {
        AnimatorKeyClipboardCommands.Copy(Session);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("SELECT KEYS"));
    }

    [Test]
    public void PastingWithoutAClipboardSaysSo()
    {
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("COPY KEYS"));
    }
}
