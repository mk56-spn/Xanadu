using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Timeline;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>The animator hosting the shared ease picker from its EASING header button.</summary>
[TestFixture]
public sealed class AnimatorEasingWindowTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorEasingWindowSystem());
        Root.Add(new EasePickerSystem());
    }

    private AnimatorEasingWindow Window => Session.GetComponent<AnimatorEasingWindow>();

    private EasePickerParts Parts => Ui.EasePicker.GetComponent<EasePicker>().Parts;

    private void SelectHeadKey(float time)
    {
        ref AnimatorSelection selection = ref Session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = XafChannelTarget.Bone;
        selection.KeyId = "head";
        selection.KeyTime = time;
    }

    private static bool Hidden(Entity entity) => !entity.Enabled;

    [Test]
    public void TheHeaderButtonOpensAndClosesThePicker()
    {
        Assert.That(Hidden(Ui.EasePicker), Is.True);

        Click(Ui.EasingButton);
        Frame();

        Assert.That(Window.Open, Is.True);
        Assert.That(Hidden(Ui.EasePicker), Is.False);
        Assert.That(Hidden(Parts.Curve), Is.False);

        Click(Ui.EasingButton);
        Frame();

        Assert.That(Hidden(Ui.EasePicker), Is.True);
        Assert.That(Hidden(Parts.Dot), Is.True);
    }

    [Test]
    public void TheCrossClosesThePicker()
    {
        Click(Ui.EasingButton);
        Frame();

        Click(Parts.Close);
        Frame();

        Assert.That(Window.Open, Is.False);
        Assert.That(Hidden(Ui.EasePicker), Is.True);
    }

    [Test]
    public void ThePickerOpensAboveItsButtonAndOnScreen()
    {
        Click(Ui.EasingButton);
        Frame(2);

        Rect2 picker = AnimatorGeometry.PanelArea(Ui.EasePicker);
        Rect2 button = AnimatorGeometry.PanelArea(Ui.EasingButton);
        Assert.That(picker.End.Y, Is.LessThanOrEqualTo(button.Position.Y));
        Assert.That(picker.Position.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(picker.End.X, Is.LessThanOrEqualTo(Screen.X));
    }

    [Test]
    public void ClickingACellSetsTheCurveAndThePickerStaysOpen()
    {
        SelectHeadKey(1f);
        Click(Ui.EasingButton);
        Frame();

        Click(EaseCell(EasePreset.BackOut));
        Frame();

        Assert.That(XafEasing.Read(Idle, XafChannelTarget.Bone, "head", 1f), Is.EqualTo(EasePreset.BackOut));
        Assert.That(Window.Open, Is.True);
        Assert.That(Ui.EasingButton.GetComponent<Text>().Value, Is.EqualTo("EASE: BACK OUT"));
        Assert.That(Parts.Name.GetComponent<Text>().Value, Is.EqualTo("BACK OUT"));
        Assert.That(EaseCell(EasePreset.BackOut).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Xanadu.Theme.Palette.Selected));
    }

    [Test]
    public void WithNoKeySelectedTheButtonSaysEasingAndTheHintSaysWhatToDo()
    {
        Click(Ui.EasingButton);
        Frame();

        Assert.That(Ui.EasingButton.GetComponent<Text>().Value, Is.EqualTo("EASING"));
        Assert.That(Parts.Hint.GetComponent<Text>().Value, Does.Contain("Select keys"));
    }

    [Test]
    public void EveryFamilyHasAnInOutAndInOutCell()
    {
        foreach (CurveFamily family in EaseNames.Families)
        {
            foreach (CurveDirection direction in EaseNames.Directions)
                Assert.That(EaseCell(EasePresets.Preset(family, direction)).IsNull, Is.False);
        }
    }
}
