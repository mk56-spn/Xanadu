using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Timeline;
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
        Root.Add(new Xanadu.Systems.Ui.EasePickerSystem());
    }

    /// <summary>The picker only takes picks while it is open.</summary>
    [SetUp]
    public void OpenPicker() => Session.GetComponent<AnimatorEasingWindow>().Open = true;

    private void SelectHeadKey(float time)
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = XafChannelTarget.Bone;
        selection.KeyId = "head";
        selection.KeyTime = time;
    }

    private EasePreset? HeadEase(float time) =>
        XafEasing.Read(Idle, XafChannelTarget.Bone, "head", time);

    private Entity PresetButton(EasePreset preset) => EaseCell(preset);

    [Test]
    public void ThePickerHasACellForEveryPreset()
    {
        foreach (EasePreset preset in System.Enum.GetValues<EasePreset>())
            Assert.That(PresetButton(preset).IsNull, Is.False);
    }

    [Test]
    public void ClickingAPresetSetsItOnTheSelectedKeySet()
    {
        SelectHeadKey(1f);

        Click(PresetButton(EasePreset.Ease));

        Assert.That(HeadEase(1f), Is.EqualTo(EasePreset.Ease));
        Assert.That(HeadEase(0f), Is.EqualTo(EasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("KEY EASE"));
    }

    [Test]
    public void WithoutASelectedKeyItAsksForOneAndChangesNothing()
    {
        Click(PresetButton(EasePreset.Step));

        Assert.That(HeadEase(0f), Is.EqualTo(EasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.StartWith("SELECT A KEY"));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.False);
    }

    [Test]
    public void ChangingAPresetIsUndoableAndMarksTheDocumentDirty()
    {
        SelectHeadKey(1f);

        Click(PresetButton(EasePreset.Step));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.True);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));
        Assert.That(HeadEase(1f), Is.EqualTo(EasePreset.Linear));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.False);
    }

    [Test]
    public void ReapplyingTheSamePresetRecordsNoHistory()
    {
        SelectHeadKey(1f);
        Click(PresetButton(EasePreset.Step));
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Click(PresetButton(EasePreset.Linear));

        Assert.That(Session.GetComponent<AnimatorHistory>().History.CanUndo, Is.False);
    }

    [Test]
    public void TheButtonMatchingTheSelectedKeyIsHighlighted()
    {
        SelectHeadKey(1f);
        Click(PresetButton(EasePreset.EaseOut));
        Frame();

        Assert.That(PresetButton(EasePreset.EaseOut).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Palette.Selected));
        Assert.That(PresetButton(EasePreset.Step).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(EasePickerPanel.Cell));

        Session.GetComponent<AnimatorSelection>().HasKey = false;
        Frame();
        Assert.That(PresetButton(EasePreset.EaseOut).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(EasePickerPanel.Cell));
    }

    [Test]
    public void ThePlayheadFollowsTheChosenCurve()
    {
        SelectHeadKey(0f);
        Click(PresetButton(EasePreset.Step));

        Runtime.Seek(0.5f);

        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(0f).Within(0.0001f));
        Runtime.Seek(1f);
        Assert.That(Runtime.Pose.Bones[2].Rotation, Is.EqualTo(1f).Within(0.0001f));
    }
}
