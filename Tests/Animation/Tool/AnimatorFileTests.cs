using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorFileTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new TextFieldFocusSystem());
        Root.Add(new TextFieldSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorShortcutSystem(Keyboard));
        Root.Add(new AnimatorFileHeaderSystem());
    }

    private void Run(ToolActionKind kind) =>
        AnimatorCommands.Run(Session, new ToolAction(kind));

    private AnimatorFile File => Session.GetComponent<AnimatorFile>();

    private ref TextField PathText => ref Ui.PathField.GetComponent<TextField>();

    private void Edit() => AnimatorEditing.AddBone(Session);

    private string Status => Session.GetComponent<AnimatorStatus>().Message;

    [Test]
    public void ADocumentStartsClean()
    {
        Assert.That(File.Dirty, Is.False);
        Assert.That(File.Path, Is.EqualTo("res://test.xaf"));
    }

    [Test]
    public void PackageEditsMakeItDirtyButPoseOnlyChangesDoNot()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 1;
        Run(ToolActionKind.RotateRight);
        Assert.That(File.Dirty, Is.False);

        Run(ToolActionKind.ToggleMovable);

        Assert.That(File.Dirty, Is.True);
    }

    [Test]
    public void SavingWritesToThePathAndMakesItCleanAgain()
    {
        Edit();

        Run(ToolActionKind.Save);

        Assert.That(Files.SavedPath, Is.EqualTo("res://test.xaf"));
        Assert.That(Files.Saved, Is.SameAs(Package));
        Assert.That(File.Dirty, Is.False);
        Assert.That(Status, Is.EqualTo("SAVED res://test.xaf"));
    }

    [Test]
    public void AFailedSaveLeavesTheDocumentDirty()
    {
        Edit();
        Files.FailSave = true;

        Run(ToolActionKind.Save);

        Assert.That(File.Dirty, Is.True);
        Assert.That(Status, Does.StartWith("SAVE FAILED"));
    }

    [Test]
    public void UndoingBackToTheSavedStateIsCleanAndRedoingIsDirty()
    {
        Edit();
        Run(ToolActionKind.Save);
        Edit();
        Assert.That(File.Dirty, Is.True);

        Run(ToolActionKind.Undo);
        Assert.That(File.Dirty, Is.False);

        Run(ToolActionKind.Redo);
        Assert.That(File.Dirty, Is.True);
    }

    [Test]
    public void LoadingWithUnsavedChangesWarnsFirstAndTheSecondPressDiscards()
    {
        Edit();
        Files.ToLoad = XafRigAuthoring.CreateEmptyPackage();

        Run(ToolActionKind.Load);
        Assert.That(Files.LoadedPath, Is.Null);
        Assert.That(Status, Does.StartWith("UNSAVED CHANGES"));
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));

        Run(ToolActionKind.Load);
        Assert.That(Files.LoadedPath, Is.EqualTo("res://test.xaf"));
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(1));
        Assert.That(File.Dirty, Is.False);
    }

    [Test]
    public void AnyOtherActionWithdrawsThePendingDiscard()
    {
        Edit();
        Files.ToLoad = XafRigAuthoring.CreateEmptyPackage();
        Run(ToolActionKind.Load);

        Run(ToolActionKind.Play);
        Run(ToolActionKind.Load);

        Assert.That(Files.LoadedPath, Is.Null);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));
    }

    [Test]
    public void SavingFirstMeansNoWarning()
    {
        Edit();
        Run(ToolActionKind.Save);
        Files.ToLoad = XafRigAuthoring.CreateEmptyPackage();

        Run(ToolActionKind.Load);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(1));
    }

    [Test]
    public void NewSkeletonAlsoAsksBeforeDiscardingAndThenStartsClean()
    {
        Edit();

        Run(ToolActionKind.NewSkeleton);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(4));

        Run(ToolActionKind.NewSkeleton);
        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(1));
        Assert.That(File.Dirty, Is.False);

        Run(ToolActionKind.NewSkeleton);
        Assert.That(Status, Is.EqualTo("NEW SKELETON"));
    }

    [Test]
    public void ALoadThatFindsNothingKeepsTheDocumentAndNamesThePath()
    {
        Files.ToLoad = null;

        Run(ToolActionKind.Load);

        Assert.That(Package.Skeleton.Bones, Has.Count.EqualTo(3));
        Assert.That(Status, Is.EqualTo("LOAD FAILED: res://test.xaf"));
    }

    [Test]
    public void PathsAreTrimmedAndGetTheExtensionWhenMissing()
    {
        Assert.That(AnimatorFileCommands.NormalizePath("  user://rig  "), Is.EqualTo("user://rig.xaf"));
        Assert.That(AnimatorFileCommands.NormalizePath("res://a/b.v2/rig"), Is.EqualTo("res://a/b.v2/rig.xaf"));
        Assert.That(AnimatorFileCommands.NormalizePath("res://a/rig.xaf"), Is.EqualTo("res://a/rig.xaf"));
        Assert.That(AnimatorFileCommands.NormalizePath("res://a/rig.bin"), Is.EqualTo("res://a/rig.bin"));
        Assert.That(AnimatorFileCommands.NormalizePath("   "), Is.EqualTo(""));
    }

    [Test]
    public void TheHeaderFieldShowsThePathAndEditingItIsSaveAs()
    {
        Frame();
        Assert.That(PathText.Value, Is.EqualTo("res://test.xaf"));

        Ui.PathField.AddTag<TextFieldFocused>();
        Frame();
        PathText.Value = "user://other";
        Frame();
        Assert.That(File.Path, Is.EqualTo("res://test.xaf"), "applied only when editing ends");
        Ui.PathField.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(File.Path, Is.EqualTo("user://other.xaf"));
        Assert.That(PathText.Value, Is.EqualTo("user://other.xaf"));

        Run(ToolActionKind.Save);
        Assert.That(Files.SavedPath, Is.EqualTo("user://other.xaf"));
    }

    [Test]
    public void AnEmptyPathIsRefusedAndTheFieldReverts()
    {
        Ui.PathField.AddTag<TextFieldFocused>();
        Frame();
        PathText.Value = "  ";
        Ui.PathField.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(File.Path, Is.EqualTo("res://test.xaf"));
        Assert.That(PathText.Value, Is.EqualTo("res://test.xaf"));
    }

    [Test]
    public void TheHeaderFlagsUnsavedChanges()
    {
        Frame();
        Assert.That(Ui.DirtyLabel.GetComponent<Text>().Value, Is.EqualTo(""));

        Edit();
        Frame();
        Assert.That(Ui.DirtyLabel.GetComponent<Text>().Value, Is.EqualTo("● UNSAVED"));

        Run(ToolActionKind.Save);
        Frame();
        Assert.That(Ui.DirtyLabel.GetComponent<Text>().Value, Is.EqualTo(""));
    }

    [Test]
    public void ControlSSaves()
    {
        Edit();

        Keyboard.Press(Key.Ctrl);
        Keyboard.Press(Key.S);
        Frame(3);

        Assert.That(Files.SavedPath, Is.EqualTo("res://test.xaf"));
        Assert.That(File.Dirty, Is.False);
    }

    [Test]
    public void ControlSIsIgnoredWhileTypingInAField()
    {
        Edit();
        Ui.PathField.AddTag<TextFieldFocused>();

        Keyboard.Press(Key.Ctrl);
        Keyboard.Press(Key.S);
        Frame(2);

        Assert.That(Files.SavedPath, Is.Null);
    }
}
