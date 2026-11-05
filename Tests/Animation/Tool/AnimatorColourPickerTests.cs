using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorColourPickerTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiColorFieldSystem());
        Root.Add(new AnimatorSelfModulateSystem());
    }

    /// <summary>What the picker system leaves behind over a drag; pointer handling has its own tests.</summary>
    private void DragColour()
    {
        Package.Attachments.Add(new XafAttachment { Group = "art", BoneId = "head" });
        Session.GetComponent<AnimatorSelection>().Attachment = 0;
        Frame();

        // The tabs have no system here, so nothing has shown the POSE tab the field is on.
        for (Entity part = Ui.ModulateField; !part.IsNull; part = part.Parent)
            UiLayout.SetHidden(part, false);
        Frame();

        Entity square = Ui.ModulateField.GetComponent<UiColorField>().Picker;
        square.GetComponent<UiColorPicker>().Dragging = true;
        Frame();
        square.GetComponent<UiColorPicker>().Color = Colors.Red;
        Frame();
        Assert.That(
            Package.Attachments[0].SelfModulate,
            Is.EqualTo(Colors.Red).Using<Color>((a, b) => a.IsEqualApprox(b) ? 0 : 1),
            "the attachment takes the colour before the drag ends");

        square.GetComponent<UiColorPicker>().Dragging = false;
        Frame();
    }

    [Test]
    public void DraggingThePickerColoursTheSelectedAttachmentInOneUndoStep()
    {
        DragColour();

        Color colour = Package.Attachments[0].SelfModulate;
        Assert.That(colour, Is.EqualTo(Colors.Red).Using<Color>((a, b) => a.IsEqualApprox(b) ? 0 : 1));

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(Package.Attachments[0].SelfModulate, Is.EqualTo(Colors.White));
    }
}
