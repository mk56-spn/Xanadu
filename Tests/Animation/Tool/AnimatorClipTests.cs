using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Ui;
using Writer.Tests.Support;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorClipTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new TextFieldFocusSystem());
        Root.Add(new TextFieldSystem());
        Root.Add(new AnimatorActionSystem());
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

    [Test]
    public void NewClipIsAddedAndShownFromItsStart()
    {
        Runtime.Seek(1.5f);

        Run(ToolActionKind.NewClip);

        Assert.That(ClipIds(), Is.EqualTo(new[] { "idle", "jump", "clip" }));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("clip"));
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0f));
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

        Assert.That(Idle.Loop, Is.True);
        Assert.That(Package.Clips[1].Loop, Is.False);

        Run(ToolActionKind.ToggleLoop);
        Assert.That(Idle.Loop, Is.False);
    }

    [Test]
    public void ClipEditsAreUndoable()
    {
        Run(ToolActionKind.NewClip);
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
        Run(ToolActionKind.NewClip);
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
