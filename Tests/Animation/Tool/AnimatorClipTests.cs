using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorClipTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new TextFieldFocusSystem());
        Root.Add(new TextFieldSystem());
        Root.Add(new ToggleSystem());
        Root.Add(new AnimatorClipConfigSystem(Pointer));
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorNewClipDialogSystem(Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorClipHeaderSystem());
    }

    private void Run(ToolActionKind kind) =>
        AnimatorCommands.Run(Session, new ToolAction(kind));

    private string[] ClipIds() => Package.Clips.ConvertAll(clip => clip.Id).ToArray();

    private ref TextField NameField => ref Ui.ClipName.GetComponent<TextField>();

    private void BeginEditing() { Ui.ClipName.AddTag<TextFieldFocused>(); Frame(); }

    private void EndEditing() { Ui.ClipName.RemoveTag<TextFieldFocused>(); Frame(); }

    private void OpenLengthPanel()
    {
        Run(ToolActionKind.OpenClipLength);
        Frame();
    }

    private void OpenNewClipDialog()
    {
        Run(ToolActionKind.OpenNewClip);
        Frame();
    }

    private static void SetField(Entity field, string value) =>
        field.GetComponent<TextField>().Value = value;

    private void PressAt(Vector2 point)
    {
        Pointer.Position = point;
        Pointer.Pressed = true;
        Frame();
        Pointer.Pressed = false;
        Frame();
    }

    [Test]
    public void OpeningTheNewClipDialogSeedsItsFieldsAndCreatesNothing()
    {
        Runtime.Seek(1.5f);

        OpenNewClipDialog();

        Assert.That(Package.Clips, Has.Count.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.True);
        Assert.That(Ui.NewClipName.GetComponent<TextField>().Value, Is.EqualTo("clip"));
        Assert.That(Ui.NewClipLength.GetComponent<TextField>().Value, Is.EqualTo("1.00"));
        Assert.That(Ui.NewClipDuplicate.GetComponent<Toggle>().Value, Is.False);
        Assert.That(Ui.NewClipName.Tags.Has<TextFieldFocused>(), Is.True);
    }

    [Test]
    public void CreatingAClipFromTheDialogUsesItsNameAndLength()
    {
        Runtime.Seek(1.5f);
        OpenNewClipDialog();
        SetField(Ui.NewClipName, "attack");
        SetField(Ui.NewClipLength, "1.50");

        Click(Button(ToolActionKind.CreateClip));

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "jump", "attack" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("attack"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0f));
        Assert.That(Package.Clips[^1].Duration, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(Package.Clips[^1].Channels, Is.Empty);
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.False);
    }

    [Test]
    public void DuplicatingFromTheDialogCopiesTheCurrentClipsKeys()
    {
        OpenNewClipDialog();

        Click(Ui.NewClipDuplicate);

        Assert.That(Ui.NewClipDuplicate.GetComponent<Toggle>().Value, Is.True);
        Assert.That(Ui.NewClipName.GetComponent<TextField>().Value, Is.EqualTo("idle-2"));
        Assert.That(Ui.NewClipLength.GetComponent<TextField>().Value, Is.EqualTo("2.00"));

        SetField(Ui.NewClipName, "stand");
        Click(Button(ToolActionKind.CreateClip));

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "stand", "jump" }));
        Assert.That(Package.Clips[1].Channels, Has.Count.EqualTo(Idle.Channels.Count));
        Assert.That(Package.Clips[1].Duration, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("stand"));
    }

    [Test]
    public void ATakenNameKeepsTheDialogOpenWithTheReason()
    {
        OpenNewClipDialog();
        SetField(Ui.NewClipName, "jump");

        Click(Button(ToolActionKind.CreateClip));

        Assert.That(Package.Clips, Has.Count.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.True);
        Assert.That(Ui.NewClipError.GetComponent<Text>().Value, Is.EqualTo("NAME ALREADY USED"));
    }

    [Test]
    public void ABadLengthKeepsTheDialogOpenWithTheReason()
    {
        OpenNewClipDialog();
        SetField(Ui.NewClipLength, "soon");

        Click(Button(ToolActionKind.CreateClip));

        Assert.That(Package.Clips, Has.Count.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.True);
        Assert.That(Ui.NewClipError.GetComponent<Text>().Value,
            Is.EqualTo("LENGTH MUST BE A NUMBER"));
    }

    [Test]
    public void CancelClosesTheNewClipDialogWithoutCreatingAnything()
    {
        OpenNewClipDialog();

        Click(Button(ToolActionKind.CloseNewClip));

        Assert.That(Package.Clips, Has.Count.EqualTo(2));
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.False);
    }

    [Test]
    public void TheNewClipButtonLivesInTheFileMenu()
    {
        List<Entity> buttons = Buttons(ToolActionKind.OpenNewClip);

        Assert.That(buttons, Has.Count.EqualTo(1));
        Assert.That(buttons[0].HasComponent<DropdownOption>(), Is.True);
        Assert.That(buttons[0].GetComponent<Text>().Value, Is.EqualTo("NEW CLIP…"));
    }

    [Test]
    public void CopyDuplicatesTheCurrentClipWithItsKeys()
    {
        Run(ToolActionKind.DuplicateClip);

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "idle-2", "jump" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle-2"));
        Assert.That(Package.Clips[1].Channels, Has.Count.EqualTo(Idle.Channels.Count));
    }

    [Test]
    public void DeleteRemovesTheCurrentClipAndShowsANeighbour()
    {
        Run(ToolActionKind.DuplicateClip);

        Run(ToolActionKind.DeleteClip);

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "jump" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }

    [Test]
    public void TheLastClipCannotBeDeleted()
    {
        Run(ToolActionKind.DeleteClip);
        Run(ToolActionKind.DeleteClip);
        Assert.That(ClipIds(), Has.Length.EqualTo(1));

        Run(ToolActionKind.DeleteClip);

        Assert.That(ClipIds(), Has.Length.EqualTo(1));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("LAST CLIP"));
    }

    [Test]
    public void LengthAndDeleteLiveOnlyInTheConfigMenu()
    {
        foreach (ToolActionKind kind in new[]
                 {
                     ToolActionKind.OpenClipLength,
                     ToolActionKind.DeleteClip,
                 })
        {
            List<Entity> controls = Buttons(kind);

            Assert.That(controls, Has.Count.EqualTo(1), kind.ToString());
            Assert.That(controls[0].HasComponent<DropdownOption>(), Is.True, kind.ToString());
        }

        Assert.That(Button(ToolActionKind.OpenClipLength).GetComponent<Text>().Value,
            Is.EqualTo("LENGTH…"));
    }

    [Test]
    public void TheLengthPanelHasThreeStepsEachSide()
    {
        OpenLengthPanel();

        List<Entity> shorter = Buttons(ToolActionKind.DurationDown);
        List<Entity> longer = Buttons(ToolActionKind.DurationUp);
        Assert.That(shorter, Has.Count.EqualTo(3));
        Assert.That(longer, Has.Count.EqualTo(3));

        foreach (Entity button in shorter)
        {
            Assert.That(IsInside(button, Ui.ClipLengthPanel), Is.True);
            Assert.That(button.HasComponent<DropdownOption>(), Is.False);
        }

        foreach (Entity button in longer)
            Assert.That(IsInside(button, Ui.ClipLengthPanel), Is.True);

        Assert.That(IsInside(Ui.ClipLengthField, Ui.ClipLengthPanel), Is.True);
        Assert.That(Ui.ClipLengthField.GetComponent<TextField>().Value, Is.EqualTo("2.00"));
        Assert.That(Button(ToolActionKind.DurationDown, "0.1").GetComponent<Text>().Value,
            Is.EqualTo("◀"));
        Assert.That(Button(ToolActionKind.DurationDown, "1").GetComponent<Text>().Value,
            Is.EqualTo("◀◀"));
        Assert.That(Button(ToolActionKind.DurationDown, "10").GetComponent<Text>().Value,
            Is.EqualTo("◀◀◀"));
    }

    [Test]
    public void ChoosingAnArrowStepsTheLengthByItsArrows()
    {
        OpenLengthPanel();

        Click(Button(ToolActionKind.DurationUp, "1"));
        Assert.That(Idle.Duration, Is.EqualTo(3f).Within(0.0001f));

        Click(Button(ToolActionKind.DurationDown, "0.1"));
        Assert.That(Idle.Duration, Is.EqualTo(2.9f).Within(0.0001f));

        Click(Button(ToolActionKind.DurationUp, "10"));
        Assert.That(Idle.Duration, Is.EqualTo(12.9f).Within(0.0001f));
    }

    [Test]
    public void TypingALengthInThePanelSetsTheClipLength()
    {
        OpenLengthPanel();
        Ui.ClipLengthField.AddTag<TextFieldFocused>();
        Frame();
        SetField(Ui.ClipLengthField, "3.25");
        Frame();

        Ui.ClipLengthField.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(Idle.Duration, Is.EqualTo(3.25f).Within(0.0001f));
        Assert.That(Ui.ClipLengthField.GetComponent<TextField>().Value, Is.EqualTo("3.25"));
    }

    [Test]
    public void ClickingOutsideTheLengthPanelClosesIt()
    {
        OpenLengthPanel();
        Assert.That(Session.GetComponent<AnimatorClipLength>().Open, Is.True);

        PressAt(AnimatorGeometry.PanelArea(Ui.Preview).GetCenter());

        Assert.That(Session.GetComponent<AnimatorClipLength>().Open, Is.False);
        Assert.That(Ui.ClipLengthPanel.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void LengthButtonsStepTheDurationButNeverCutKeysOff()
    {
        Run(ToolActionKind.DurationUp);
        Assert.That(Idle.Duration, Is.EqualTo(2.1f).Within(0.0001f));

        for (int i = 0; i < 30; i++)
            Run(ToolActionKind.DurationDown);

        // The last head key sits at 2.0 s.
        Assert.That(Idle.Duration, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(XafValidation.Validate(Package), Is.Empty);
    }

    [Test]
    public void ShorteningPullsThePlayheadBackInside()
    {
        XafClip jump = Package.Clips[1];
        jump.Duration = 1f;
        Run(ToolActionKind.DuplicateClip);
        Runtime.Seek(0.95f);
        Package.Clips[Package.Clips.FindIndex(clip => clip.Id == Runtime.CurrentClip)].Duration = 1f;

        Run(ToolActionKind.DurationDown);

        Assert.That(Runtime.CurrentTime, Is.LessThanOrEqualTo(Runtime.CurrentDuration + 0.0001f));
    }

    [Test]
    public void LoopButtonTogglesLoopingForTheCurrentClipOnly()
    {
        Run(ToolActionKind.ToggleLoop);
        Frame();

        Assert.That(Idle.Loop, Is.True);
        Assert.That(Package.Clips[1].Loop, Is.False);
        Assert.That(Ui.LoopButton.GetComponent<Text>().Value, Is.EqualTo("LOOP ON"));

        Run(ToolActionKind.ToggleLoop);
        Frame();
        Assert.That(Idle.Loop, Is.False);
        Assert.That(Ui.LoopButton.GetComponent<Text>().Value, Is.EqualTo("LOOP OFF"));
    }

    [Test]
    public void ClipEditsAreUndoable()
    {
        AnimatorClipCommands.Create(Session, "clip", 1f, false);
        Run(ToolActionKind.DurationUp);
        Run(ToolActionKind.ToggleLoop);

        Run(ToolActionKind.Undo);
        Run(ToolActionKind.Undo);
        Assert.That(Package.Clips[^1].Duration, Is.EqualTo(1f));
        Assert.That(Package.Clips[^1].Loop, Is.False);

        Run(ToolActionKind.Undo);
        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "jump" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }

    [Test]
    public void TheClipDropdownListsNewClipsAndMarksTheCurrentOne()
    {
        AnimatorClipCommands.Create(Session, "clip", 1f, false);
        Frame(2);

        Color current = Button(ToolActionKind.Clip, "clip").GetComponent<ButtonVisual>().Normal;
        Color other = Button(ToolActionKind.Clip, "idle").GetComponent<ButtonVisual>().Normal;
        Assert.That(current, Is.EqualTo(UiTheme.Selected));
        Assert.That(other, Is.Not.EqualTo(current));
    }

    [Test]
    public void TheNameFieldShowsTheCurrentClip()
    {
        Assert.That(NameField.Value, Is.EqualTo("idle"));

        Click(Button(ToolActionKind.Clip, "jump"));

        Assert.That(NameField.Value, Is.EqualTo("jump"));
    }

    [Test]
    public void FinishingAnEditRenamesTheClip()
    {
        BeginEditing();
        NameField.Value = "stand";
        Frame();
        Assert.That(Idle.Id, Is.EqualTo("idle"), "nothing is applied while typing");

        EndEditing();

        Assert.That(Idle.Id, Is.EqualTo("stand"));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("stand"));
        Assert.That(NameField.Value, Is.EqualTo("stand"));
    }

    [Test]
    public void ATakenOrEmptyNameIsRefusedAndTheFieldReverts()
    {
        BeginEditing();
        NameField.Value = "jump";
        EndEditing();

        Assert.That(Idle.Id, Is.EqualTo("idle"));
        Assert.That(NameField.Value, Is.EqualTo("idle"));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Does.Contain("ALREADY USED"));

        BeginEditing();
        NameField.Value = "   ";
        EndEditing();
        Assert.That(Idle.Id, Is.EqualTo("idle"));
    }

    [Test]
    public void SwitchingClipsMidEditRenamesTheClipThatWasBeingEdited()
    {
        BeginEditing();
        NameField.Value = "stand";

        // Pressing a clip button takes focus away and switches clips in one frame.
        Ui.ClipName.RemoveTag<TextFieldFocused>();
        Click(Button(ToolActionKind.Clip, "jump"));

        Assert.That(ClipIds(), Is.EqualTo(new[] { "stand", "jump" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("jump"));
    }

    [Test]
    public void ARenameIsUndoable()
    {
        BeginEditing();
        NameField.Value = "stand";
        EndEditing();

        Run(ToolActionKind.Undo);

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "jump" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }

    [Test]
    public void ShortcutsAreIgnoredWhileTypingAName()
    {
        Session.GetComponent<AnimatorSelection>().HasKey = true;
        Session.GetComponent<AnimatorSelection>().KeyTarget = XafChannelTarget.Bone;
        Session.GetComponent<AnimatorSelection>().KeyId = "head";
        Session.GetComponent<AnimatorSelection>().KeyTime = 1f;
        Ui.ClipName.AddTag<TextFieldFocused>();

        Keyboard.Press(Godot.Key.Delete);
        Frame(2);

        Assert.That(XafTracks.KeyTimes(Idle, new XafTrack(XafChannelTarget.Bone, "head")), Has.Count.EqualTo(3));
    }
}
