using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Short button labels stay understandable through hover explanations.</summary>
[TestFixture]
public sealed class AnimatorTooltipTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorTooltipSystem(Pointer));
    }

    private Entity Tooltip => Ui.Tooltip;

    private string TooltipText => Tooltip.GetComponent<Text>().Value;

    private Rect2 TooltipArea() => AnimatorGeometry.PanelArea(Tooltip);

    private void Hover(Entity button, Vector2 pointer)
    {
        button.RemoveTag<Hovered>();
        button.AddTag<Hovered>();
        Pointer.Position = pointer;
        Frame();
    }

    [Test]
    public void TheTooltipDrawsAboveEveryPanelAndCannotTakeInput()
    {
        Assert.That(
            Tooltip.GetComponent<Drawable>().ZIndex,
            Is.EqualTo(AnimatorLayout.TooltipLayer));
        Assert.That(AnimatorLayout.TooltipLayer, Is.GreaterThan(AnimatorLayout.MenuLayer));
        Assert.That(Tooltip.Tags.Has<TakesInput>(), Is.False);
    }

    [Test]
    public void HoveringAButtonShowsItsExplanationNearThePointer()
    {
        Entity play = Button(ToolActionKind.Play);

        Hover(play, new Vector2(500, 300));

        Assert.That(TooltipText, Is.EqualTo("Play from the playhead (Space)"));
        Assert.That(TooltipArea().Position.X, Is.GreaterThan(500));
        Assert.That(TooltipArea().Position.Y, Is.GreaterThan(300));
    }

    [Test]
    public void TheExplanationGoesBackInsideTheScreenAtTheEdges()
    {
        Entity play = Button(ToolActionKind.Play);

        Hover(play, new Vector2(Screen.X - 2, Screen.Y - 2));

        Rect2 area = TooltipArea();
        Assert.That(area.Position.X, Is.GreaterThanOrEqualTo(0f));
        Assert.That(area.Position.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(area.End.X, Is.LessThanOrEqualTo(Screen.X));
        Assert.That(area.End.Y, Is.LessThanOrEqualTo(Screen.Y));
    }

    [Test]
    public void PressingThePointerHidesTheExplanation()
    {
        Entity play = Button(ToolActionKind.Play);
        Hover(play, new Vector2(500, 300));

        Pointer.Pressed = true;
        Frame();

        Assert.That(TooltipText, Is.Empty);
    }

    [Test]
    public void LeavingTheButtonHidesTheExplanation()
    {
        Entity play = Button(ToolActionKind.Play);
        Hover(play, new Vector2(500, 300));

        play.RemoveTag<Hovered>();
        Frame();

        Assert.That(TooltipText, Is.Empty);
        Assert.That(TooltipArea().Position.Y, Is.LessThan(0f));
    }

    [Test]
    public void EveryActionButtonExplainsItself()
    {
        foreach (Entity entity in Store.Query<ToolAction>().ToEntityList())
        {
            ToolAction action = entity.GetComponent<ToolAction>();
            Assert.That(
                entity.HasComponent<UiTooltip>(),
                Is.True,
                $"{action.Kind} '{action.Argument}' has no hover text");
            Assert.That(entity.GetComponent<UiTooltip>().Text, Is.Not.Empty);
        }
    }

    [Test]
    public void EveryModeToggleExplainsItself()
    {
        foreach (Entity entity in Store.Query<Toggle, ModeToggle>().ToEntityList())
        {
            AnimatorMode mode = entity.GetComponent<ModeToggle>().Mode;
            Assert.That(
                entity.HasComponent<UiTooltip>(),
                Is.True,
                $"{mode} has no hover text");
            Assert.That(entity.GetComponent<UiTooltip>().Text, Is.Not.Empty);
        }
    }

    [Test]
    public void TheAbbreviatedRotationButtonStillExplainsItsShortcut()
    {
        Entity rotate = Button(ToolActionKind.RotateLeft);

        Assert.That(
            rotate.GetComponent<UiTooltip>().Text,
            Is.EqualTo("Turn selected bones -0.25 rad (Q)"));
    }
}
