using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorEasingWindowTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorEasingWindowSystem());
        Root.Add(new AnimatorInterpolationSystem());
    }

    private AnimatorEasingWindow Window => Session.GetComponent<AnimatorEasingWindow>();

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
    public void TheHeaderButtonOpensAndClosesTheWindow()
    {
        Assert.That(Hidden(Ui.EasingWindow), Is.True);

        Click(Ui.EasingButton);
        Frame();

        Assert.That(Window.Open, Is.True);
        Assert.That(Hidden(Ui.EasingWindow), Is.False);
        Assert.That(Hidden(Ui.EasingCurve), Is.False);

        Click(Ui.EasingButton);
        Frame();

        Assert.That(Hidden(Ui.EasingWindow), Is.True);
        Assert.That(Hidden(Ui.EasingDot), Is.True);
    }

    [Test]
    public void TheWindowOpensAboveItsButtonAndOnScreen()
    {
        Click(Ui.EasingButton);
        Frame(2);

        Godot.Rect2 window = AnimatorGeometry.PanelArea(Ui.EasingWindow);
        Godot.Rect2 button = AnimatorGeometry.PanelArea(Ui.EasingButton);
        Assert.That(window.End.Y, Is.LessThanOrEqualTo(button.Position.Y));
        Assert.That(window.Position.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(window.End.X, Is.LessThanOrEqualTo(Screen.X));
    }

    [Test]
    public void ClickingACellSetsTheCurveAndTheWindowStaysOpen()
    {
        SelectHeadKey(1f);
        Click(Ui.EasingButton);
        Frame();

        Click(Button(ToolActionKind.Interpolate, nameof(XafEasePreset.BackOut)));
        Frame();

        Assert.That(XafEasing.Read(Idle, XafChannelTarget.Bone, "head", 1f), Is.EqualTo(XafEasePreset.BackOut));
        Assert.That(Window.Open, Is.True);
        Assert.That(Ui.EasingButton.GetComponent<Text>().Value, Is.EqualTo("EASE: BACK OUT"));
        Assert.That(Ui.EasingName.GetComponent<Text>().Value, Is.EqualTo("BACK OUT"));
        Assert.That(Button(ToolActionKind.Interpolate, nameof(XafEasePreset.BackOut)).GetComponent<ButtonVisual>().Normal,
            Is.EqualTo(Xanadu.Theme.Palette.Selected));
    }

    [Test]
    public void WithNoKeySelectedTheButtonSaysEasingAndTheHintSaysWhatToDo()
    {
        Click(Ui.EasingButton);
        Frame();

        Assert.That(Ui.EasingButton.GetComponent<Text>().Value, Is.EqualTo("EASING"));
        Assert.That(Ui.EasingHint.GetComponent<Text>().Value, Does.Contain("Select keys"));
    }

    [Test]
    public void EveryFamilyHasAnInOutAndInOutCell()
    {
        foreach (XafCurveFamily family in AnimatorEasingNames.Families)
        {
            foreach (XafCurveDirection direction in AnimatorEasingNames.Directions)
            {
                Entity cell = Button(ToolActionKind.Interpolate, XafEasing.Preset(family, direction).ToString());
                Assert.That(cell.Tags.Has<AnimatorEasingCell>(), Is.True);
            }
        }
    }
}
