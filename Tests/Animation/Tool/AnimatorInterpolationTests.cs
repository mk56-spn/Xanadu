using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorInterpolationTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorInterpolationSystem());
    }

    private void SelectHeadKey(float time)
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = XafChannelTarget.Bone;
        selection.KeyId = "head";
        selection.KeyTime = time;
    }

    private XafEasePreset? HeadEase(float time) =>
        XafEasing.Read(Idle, XafChannelTarget.Bone, "head", time);

    private Entity PresetButton(XafEasePreset preset) =>
        Button(ToolActionKind.Interpolate, preset.ToString());

    [Test]
    public void TheHeaderHasAButtonForEveryPreset()
    {
        foreach (XafEasePreset preset in System.Enum.GetValues<XafEasePreset>())
            Assert.That(PresetButton(preset).IsNull, Is.False);
    }

    [Test]
    public void ClickingAPresetSetsItOnTheSelectedKeySet()
    {
        SelectHeadKey(1f);

        Click(PresetButton(XafEasePreset.Ease));

        Assert.That(HeadEase(1f), Is.EqualTo(XafEasePreset.Ease));
        Assert.That(HeadEase(0f), Is.EqualTo(XafEasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("KEY EASE"));
    }

    [Test]
    public void WithoutASelectedKeyItAsksForOneAndChangesNothing()
    {
        Click(PresetButton(XafEasePreset.Step));

        Assert.That(HeadEase(0f), Is.EqualTo(XafEasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.StartWith("SELECT A KEY"));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.False);
    }

    [Test]
    public void ChangingAPresetIsUndoableAndMarksTheDocumentDirty()
    {
        SelectHeadKey(1f);

        Click(PresetButton(XafEasePreset.Step));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.True);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Assert.That(HeadEase(1f), Is.EqualTo(XafEasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.False);
    }

    [Test]
    public void ReapplyingTheSamePresetRecordsNoHistory()
    {
        SelectHeadKey(1f);
        Click(PresetButton(XafEasePreset.Step));
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Click(PresetButton(XafEasePreset.Linear));

        Assert.That(Session.GetComponent<AnimatorHistory>().History.CanUndo, Is.False);
    }

    [Test]
    public void TheButtonMatchingTheSelectedKeyIsHighlighted()
    {
        SelectHeadKey(1f);
        Click(PresetButton(XafEasePreset.EaseOut));
        Frame();

        Assert.That(PresetButton(XafEasePreset.EaseOut).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(new Color("#0f766e")));
        Assert.That(PresetButton(XafEasePreset.Step).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(UiTheme.Button));

        Session.GetComponent<AnimatorSelection>().HasKey = false;
        Frame();
        Assert.That(PresetButton(XafEasePreset.EaseOut).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(UiTheme.Button));
    }

    [Test]
    public void ThePlayheadFollowsTheChosenCurve()
    {
        SelectHeadKey(0f);
        Click(PresetButton(XafEasePreset.Step));

        Runtime.Seek(0.5f);

        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(0f).Within(0.0001f));
        Runtime.Seek(1f);
        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(1f).Within(0.0001f));
    }
}
