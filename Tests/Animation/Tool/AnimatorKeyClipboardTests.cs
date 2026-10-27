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

/// <summary>Copying, pasting and bulk-selecting key sets.</summary>
[TestFixture]
public sealed class AnimatorKeyClipboardTests : AnimatorToolTest
{
    private static readonly XafTrack HeadTrack = new(XafChannelTarget.Bone, "head");

    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private static XafChannel HeadChannel(XafClip clip) =>
        clip.Channels.Find(channel =>
            channel.Target == XafChannelTarget.Bone &&
            channel.TargetId == "head" &&
            channel.Property == XafChannelProperty.Rotation)!;

    private static XafKeyframe? KeyAt(XafClip clip, float time) =>
        HeadChannel(clip).Keyframes.Find(key =>
            System.Math.Abs(key.Time - time) <= XafTracks.TimeTolerance);

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

        XafKeyframe? key = KeyAt(Idle, 0.25f);
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.Value, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void PasteKeepsTheCurveOfTheCopiedKey()
    {
        XafEasing.Apply(Idle, XafChannelTarget.Bone, "head", 1f, XafEasePreset.Step);
        AnimatorKeyCommands.SelectKey(Session, HeadTrack, 1f);
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(0.5f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(
            KeyAt(Idle, 0.5f)!.Interpolation,
            Is.EqualTo(XafInterpolation.Step));
    }

    [Test]
    public void KeysCanBePastedIntoAnotherClip()
    {
        AnimatorKeyCommands.SelectKey(Session, HeadTrack, 1f);
        AnimatorKeyClipboardCommands.Copy(Session);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Clip, "jump"));

        AnimatorKeyClipboardCommands.Paste(Session);

        XafClip jump = Package.Clips[1];
        Assert.That(jump.Channels, Has.Count.EqualTo(1));
        Assert.That(KeyAt(jump, 0f), Is.Not.Null);
    }

    [Test]
    public void PasteMovesTheBlockBackSoItFitsTheClip()
    {
        AnimatorKeyCommands.SelectKeys(Session, new List<AnimatorKeyRef>
        {
            new(XafChannelTarget.Bone, "head", 1f),
            new(XafChannelTarget.Bone, "head", 2f),
        });
        AnimatorKeyClipboardCommands.Copy(Session);

        Runtime.Seek(2f);
        AnimatorKeyClipboardCommands.Paste(Session);

        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("AT 1.00s"));
        Assert.That(KeyAt(Idle, 1f), Is.Not.Null);
        Assert.That(KeyAt(Idle, 2f), Is.Not.Null);
        Assert.That(
            HeadChannel(Idle).Keyframes.Exists(key => key.Time > Idle.Duration + 0.0001f),
            Is.False);
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
        Press(KeyPoint("head", 1f), shift: true);
        Assert.That(Selection.Keys(), Has.Count.EqualTo(1));
        Release();

        Press(KeyPoint("head", 0f), shift: true);
        Assert.That(Selection.Keys(), Has.Count.EqualTo(2));
        Release();

        Press(KeyPoint("head", 1f), shift: true);
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
