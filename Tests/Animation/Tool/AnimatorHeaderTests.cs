using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Where the config menu, length panel, new clip dialog and undo/redo panel sit.</summary>
[TestFixture]
public sealed class AnimatorHeaderTests : AnimatorToolTest
{
    // The shared shell's header runs along the screen's top edge.
    private const float HeaderBottom = EditorShellMetrics.HeaderHeight;

    protected override void AddSystems()
    {
        Root.Add(new UiDropdownSystem(Store));
        Root.Add(new TextFieldFocusSystem());
        Root.Add(new ToggleSystem());
        Root.Add(new AnimatorClipConfigSystem(Pointer));
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorNewClipDialogSystem(Pointer));
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorClipHeaderSystem());
    }

    private static Rect2 Area(Entity entity) => AnimatorGeometry.PanelArea(entity);

    private static Vector2 Center(Entity entity) => Area(entity).GetCenter();

    private void PressAt(Vector2 point)
    {
        Pointer.Position = point;
        Pointer.Pressed = true;
        Frame();
        Pointer.Pressed = false;
        Frame();
    }

    private void OpenLengthPanel() => Click(Button(ToolActionKind.OpenClipLength));

    private void OpenNewClipDialog() => Click(Button(ToolActionKind.OpenNewClip));

    [Test]
    public void TheClipDropdownSitsInTheHeader()
    {
        Rect2 button = Area(Ui.ClipDropdown);
        Rect2 header = EditorShell
            .Compute(Screen, new EditorShellOptions(EditorFooterHeight.Animator))
            .Header;

        Assert.That(header.Encloses(button), Is.True);
        Assert.That(button.End.X, Is.LessThanOrEqualTo(header.End.X));
    }

    [Test]
    public void TheConfigButtonSitsInTheHeaderAfterTheClips()
    {
        Rect2 config = Area(Ui.ConfigButton);
        Rect2 header = EditorShell
            .Compute(Screen, new EditorShellOptions(EditorFooterHeight.Animator))
            .Header;

        Assert.That(header.Encloses(config), Is.True);
        Assert.That(config.Position.X, Is.GreaterThan(Area(Ui.ClipDropdown).End.X));
        Assert.That(config.End.X, Is.LessThanOrEqualTo(header.End.X));
        Assert.That(Ui.ConfigButton.GetComponent<Text>().Value, Is.EqualTo("CONFIG ▾"));
    }

    [Test]
    public void TheClipsAreNoLongerInTheRightPanel()
    {
        Rect2 rightPanel = new(Screen.X - 16f - 300f, HeaderBottom, 300f, Screen.Y);

        Assert.That(rightPanel.Intersects(Area(Ui.ClipDropdown)), Is.False);
    }

    [Test]
    public void OpeningTheClipListDoesNotMoveTheButton()
    {
        Rect2 before = Area(Ui.ClipDropdown);

        Ui.ClipDropdown.GetComponent<Dropdown>().Open = true;
        Frame(3);

        Assert.That(Area(Ui.ClipDropdown), Is.EqualTo(before));
    }

    [Test]
    public void TheOpenClipListDropsBelowTheButtonOnTheTopLayer()
    {
        Ui.ClipDropdown.GetComponent<Dropdown>().Open = true;
        Frame(3);

        Entity list = Ui.ClipDropdown.GetComponent<Dropdown>().List;
        Assert.That(Area(list).Position.Y, Is.EqualTo(Area(Ui.ClipDropdown).End.Y + 4f).Within(0.5f));
        Assert.That(list.GetComponent<Drawable>().ZIndex, Is.EqualTo(AnimatorLayout.MenuLayer));
        Assert.That(list.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
    }

    [Test]
    public void TheClipListStaysOnTheTopLayerAfterTheClipsChange()
    {
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.DuplicateClip));
        Frame(2);
        Ui.ClipDropdown.GetComponent<Dropdown>().Open = true;
        Frame(3);

        Rect2 button = Area(Ui.ClipDropdown);
        Entity list = Ui.ClipDropdown.GetComponent<Dropdown>().List;
        Assert.That(button.End.Y, Is.LessThanOrEqualTo(HeaderBottom));
        Assert.That(list.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
    }

    [Test]
    public void UndoAndRedoSitInTheHeaderRightOfTheName()
    {
        Rect2 undo = Area(Button(ToolActionKind.Undo));
        Rect2 redo = Area(Button(ToolActionKind.Redo));

        Assert.That(undo.End.Y, Is.LessThanOrEqualTo(EditorShellMetrics.HeaderHeight));
        Assert.That(undo.Position.X, Is.GreaterThanOrEqualTo(EditorShellMetrics.NameWidth));
        Assert.That(redo.Position.X, Is.GreaterThan(undo.End.X));
        Assert.That(redo.Position.Y, Is.EqualTo(undo.Position.Y).Within(0.5f));
    }

    [Test]
    public void UndoAndRedoShowTwoLettersUntilTheirIconsLoad()
    {
        Assert.That(Button(ToolActionKind.Undo).GetComponent<Text>().Value, Is.EqualTo("UN"));
        Assert.That(Button(ToolActionKind.Redo).GetComponent<Text>().Value, Is.EqualTo("RE"));
    }

    [Test]
    public void UndoAndRedoStillWorkFromTheHeader()
    {
        Session.GetComponent<AnimatorSelection>().Bone = 1;
        Runtime.Seek(0.5f);
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.KeyBone));
        int keyed = Idle.Channels.Count;

        Click(Button(ToolActionKind.Undo));
        Assert.That(Idle.Channels.Count, Is.LessThan(keyed));

        Click(Button(ToolActionKind.Redo));
        Assert.That(Idle.Channels.Count, Is.EqualTo(keyed));
    }

    [Test]
    public void TheLengthPanelStartsHidden()
    {
        Assert.That(Session.GetComponent<AnimatorClipLength>().Open, Is.False);
        Assert.That(Ui.ClipLengthPanel.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void TheConfigMenuOffersLengthAndDelete()
    {
        Entity length = Button(ToolActionKind.OpenClipLength);
        Entity delete = Button(ToolActionKind.DeleteClip);

        Assert.That(length.HasComponent<DropdownOption>(), Is.True);
        Assert.That(length.GetComponent<Text>().Value, Is.EqualTo("LENGTH…"));
        Assert.That(delete.HasComponent<DropdownOption>(), Is.True);
        Assert.That(delete.GetComponent<Text>().Value, Is.EqualTo("DELETE"));
    }

    [Test]
    public void ChoosingLengthDropsThePanelUnderTheConfigButton()
    {
        OpenLengthPanel();

        Assert.That(Session.GetComponent<AnimatorClipLength>().Open, Is.True);
        Rect2 panel = Area(Ui.ClipLengthPanel);
        Rect2 config = Area(Ui.ConfigButton);
        Assert.That(panel.Size, Is.EqualTo(AnimatorLayout.ClipLengthSize));
        Assert.That(panel.Position.Y, Is.GreaterThanOrEqualTo(config.End.Y));
        Assert.That(panel.End.X, Is.EqualTo(config.End.X).Within(0.5f));
        Assert.That(panel.End.Y, Is.LessThanOrEqualTo(Area(Ui.Timeline).Position.Y));
    }

    [Test]
    public void TheLengthPanelDrawsOnTheMenuLayerAndTakesInput()
    {
        OpenLengthPanel();

        Assert.That(Ui.ClipLengthPanel.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
        Assert.That(Ui.ClipLengthPanel.Tags.Has<TakesInput>(), Is.True);
        Assert.That(Ui.ClipLengthPanel.GetComponent<Drawable>().ZIndex,
            Is.EqualTo(AnimatorLayout.MenuLayer));
    }

    [Test]
    public void ClickingOutsideTheLengthPanelClosesIt()
    {
        OpenLengthPanel();

        PressAt(Area(Ui.Preview).GetCenter());

        Assert.That(Session.GetComponent<AnimatorClipLength>().Open, Is.False);
        Assert.That(Ui.ClipLengthPanel.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void TheNewClipDialogStartsHidden()
    {
        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.False);
        Assert.That(Ui.NewClipDialog.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
        Assert.That(Ui.NewClipBackdrop.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void ChoosingNewClipCentresTheDialogOverADimmer()
    {
        OpenNewClipDialog();

        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.True);
        Rect2 dialog = Area(Ui.NewClipDialog);
        Assert.That(dialog.Size, Is.EqualTo(AnimatorLayout.NewClipDialogSize));
        Assert.That(dialog.GetCenter(), Is.EqualTo(Screen / 2));
        Assert.That(Area(Ui.NewClipBackdrop),
            Is.EqualTo(new Rect2(Vector2.Zero, Screen)));
        Assert.That(Ui.NewClipBackdrop.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
        Assert.That(Ui.NewClipDialog.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
        Assert.That(Ui.NewClipDialog.Tags.Has<UiOverlayItem>(), Is.True);
        Assert.That(Ui.NewClipBackdrop.Tags.Has<UiOverlay>(), Is.True);
    }

    [Test]
    public void TheNewClipDialogBlocksWhatIsUnderIt()
    {
        OpenNewClipDialog();

        Assert.That(Ui.NewClipBackdrop.Tags.Has<TakesInput>(), Is.True);
        Assert.That(Ui.NewClipDialog.Tags.Has<TakesInput>(), Is.True);
        Assert.That(Ui.NewClipBackdrop.Id, Is.LessThan(Ui.NewClipDialog.Id));
    }

    [Test]
    public void TheNewClipDialogHasNameLengthDuplicateAndButtons()
    {
        OpenNewClipDialog();

        Assert.That(Ui.NewClipName.HasComponent<TextField>(), Is.True);
        Assert.That(Ui.NewClipLength.GetComponent<TextField>().Numeric, Is.True);
        Assert.That(Ui.NewClipDuplicate.HasComponent<Toggle>(), Is.True);
        Assert.That(IsInside(Button(ToolActionKind.CreateClip), Ui.NewClipDialog), Is.True);
        Assert.That(IsInside(Button(ToolActionKind.CloseNewClip), Ui.NewClipDialog), Is.True);
        Assert.That(IsInside(Ui.NewClipError, Ui.NewClipDialog), Is.True);
    }

    [Test]
    public void ClickingOutsideTheNewClipDialogCancelsIt()
    {
        OpenNewClipDialog();

        PressAt(new Vector2(40, 40));

        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.False);
        Assert.That(Ui.NewClipDialog.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
        Assert.That(Ui.NewClipBackdrop.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void CancelClosesTheNewClipDialog()
    {
        OpenNewClipDialog();

        Click(Button(ToolActionKind.CloseNewClip));

        Assert.That(Session.GetComponent<AnimatorNewClipDialog>().Open, Is.False);
        Assert.That(Ui.NewClipDialog.GetComponent<Position>().Value,
            Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void GamePreviewAndLoopSitAboveTheTimelineSideBySide()
    {
        Rect2 game = Area(Button(ToolActionKind.SequenceGame));
        Rect2 all = Area(Button(ToolActionKind.SequenceAll));
        Rect2 loop = Area(Ui.LoopButton);
        float rulerTop = Model().Ruler.Position.Y;

        Assert.That(game.End.Y, Is.LessThanOrEqualTo(rulerTop));
        Assert.That(all.End.Y, Is.LessThanOrEqualTo(rulerTop));
        Assert.That(loop.End.Y, Is.LessThanOrEqualTo(rulerTop));
        Assert.That(all.Position.Y, Is.EqualTo(game.Position.Y).Within(0.5f));
        Assert.That(loop.Position.Y, Is.EqualTo(game.Position.Y).Within(0.5f));
        Assert.That(all.Position.X, Is.GreaterThan(game.End.X));
        Assert.That(loop.Position.X, Is.GreaterThan(all.End.X));
    }
}
