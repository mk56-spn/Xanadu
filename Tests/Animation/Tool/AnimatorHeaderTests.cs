using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Ui;
using Writer.Tests.Support;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

/// <summary>Where the clip dropdown and the undo/redo panel sit in the animator layout.</summary>
[TestFixture]
public sealed class AnimatorHeaderTests : AnimatorToolTest
{
    // Margin 16, then the header's 88.
    private const float HeaderBottom = 104f;

    protected override void AddSystems()
    {
        Root.Add(new UiDropdownSystem(Store));
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private static Rect2 Area(Entity entity) => AnimatorGeometry.PanelArea(entity);

    [Test]
    public void TheClipDropdownSitsInTheHeader()
    {
        Rect2 button = Area(Ui.ClipDropdown);

        Assert.That(button.Position.Y, Is.GreaterThanOrEqualTo(16f));
        Assert.That(button.End.Y, Is.LessThanOrEqualTo(HeaderBottom));
        Assert.That(button.End.X, Is.LessThanOrEqualTo(Screen.X - 16f));
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
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.NewClip));
        Frame(2);
        Ui.ClipDropdown.GetComponent<Dropdown>().Open = true;
        Frame(3);

        Rect2 button = Area(Ui.ClipDropdown);
        Entity list = Ui.ClipDropdown.GetComponent<Dropdown>().List;
        Assert.That(button.End.Y, Is.LessThanOrEqualTo(HeaderBottom));
        Assert.That(list.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
    }

    [Test]
    public void UndoAndRedoFloatOverThePreviewRightOfTheBonesList()
    {
        Rect2 undo = Area(Button(ToolActionKind.Undo));
        Rect2 redo = Area(Button(ToolActionKind.Redo));
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);

        // The bones list ends at 16 + 240; the preview starts a gap later.
        Assert.That(undo.Position.X, Is.GreaterThanOrEqualTo(256f));
        Assert.That(preview.Encloses(undo), Is.True);
        Assert.That(preview.Encloses(redo), Is.True);
        Assert.That(redo.Position.X, Is.GreaterThan(undo.End.X));
        Assert.That(redo.Position.Y, Is.EqualTo(undo.Position.Y).Within(0.5f));
    }

    [Test]
    public void TheHistoryPanelDrawsAboveThePreviewAndSwallowsClicksOnItsPadding()
    {
        Entity undo = Button(ToolActionKind.Undo);
        Entity panel = undo.Parent;

        Assert.That(undo.GetComponent<Drawable>().ZIndex, Is.EqualTo(AnimatorLayout.MenuLayer));
        Assert.That(panel.Tags.Has<TakesInput>(), Is.True);
        Assert.That(panel.Parent.Id, Is.EqualTo(Ui.MenuLayer.Id));
    }

    [Test]
    public void UndoAndRedoStillWorkFromTheirPanel()
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
}
