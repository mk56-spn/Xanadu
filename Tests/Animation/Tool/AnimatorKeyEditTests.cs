using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>
/// Posing the rig while keys are selected edits those keys: the change at the
/// playhead is added to each selected key, only on the channels it touched.
/// The fixture's head has rotation keys 0, 1 and 2 at 0 s, 1 s and 2 s.
/// </summary>
[TestFixture]
public sealed class AnimatorKeyEditTests : AnimatorToolTest
{
    private static readonly XafTrack Head = new(XafChannelTarget.Bone, "head");

    private XafChannel? Channel(string bone, XafChannelProperty property) =>
        Idle.Channels.Find(channel => channel.TargetId == bone && channel.Property == property);

    private float HeadKey(float time) =>
        Channel("head", XafChannelProperty.Rotation)!.Keyframes.Find(key => key.Time == time)!.Value;

    private void Run(ToolActionKind kind) => AnimatorCommands.Run(Session, new ToolAction(kind));

    [Test]
    public void EditingWithAKeySelectedOverwritesThatKeyWithoutAutoKey()
    {
        AnimatorKeyCommands.SelectKey(Session, Head, 1f);

        Run(ToolActionKind.RotateRight);

        Assert.That(HeadKey(1f), Is.EqualTo(1f + AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(HeadKey(0f), Is.EqualTo(0f));
        Assert.That(HeadKey(2f), Is.EqualTo(2f));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.EndWith("KEY EDITED"));
    }

    [Test]
    public void OnlyTheChannelsTheEditChangedAreWritten()
    {
        AnimatorKeyCommands.SelectKey(Session, Head, 1f);

        Run(ToolActionKind.RotateRight);

        Assert.That(Channel("head", XafChannelProperty.ScaleX), Is.Null);
        Assert.That(Channel("head", XafChannelProperty.ScaleY), Is.Null);
    }

    [Test]
    public void SeveralSelectedKeysAllTakeTheSameOffset()
    {
        AnimatorKeyCommands.SelectKey(Session, Head, 0f);
        AnimatorKeyCommands.ToggleKey(Session, Head, 2f);

        Run(ToolActionKind.RotateRight);

        Assert.That(HeadKey(0f), Is.EqualTo(AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(HeadKey(2f), Is.EqualTo(2f + AnimatorEditing.RotationStep).Within(0.0001f));
        Assert.That(HeadKey(1f), Is.EqualTo(1f), "an unselected key is left alone");
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.EndWith("2 KEYS EDITED"));
    }

    [Test]
    public void EditingABoneWithoutSelectedKeysLeavesTheKeysAlone()
    {
        AnimatorKeyCommands.SelectKey(Session, Head, 1f);
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);

        Run(ToolActionKind.RotateRight);

        Assert.That(HeadKey(1f), Is.EqualTo(1f));
        Assert.That(Channel("torso", XafChannelProperty.Rotation), Is.Null, "auto-key is off, so nothing is keyed");
    }

    [Test]
    public void AutoKeyStillKeysAtThePlayheadWhenNoKeyIsSelected()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
        Session.GetComponent<AnimatorModes>().AutoKey = true;
        Runtime.Seek(0.5f);

        Run(ToolActionKind.RotateRight);

        Assert.That(Channel("torso", XafChannelProperty.Rotation)!.Keyframes[0].Time, Is.EqualTo(0.5f));
    }

    [Test]
    public void OneUndoPutsTheKeysBack()
    {
        AnimatorKeyCommands.SelectKey(Session, Head, 0f);
        AnimatorKeyCommands.ToggleKey(Session, Head, 2f);
        Run(ToolActionKind.RotateRight);

        Run(ToolActionKind.Undo);

        Assert.That(HeadKey(0f), Is.EqualTo(0f));
        Assert.That(HeadKey(2f), Is.EqualTo(2f));
    }
}
