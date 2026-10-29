using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Preview mode: the editing panels give way to the viewport and the preview bar, and come back on exit.</summary>
[TestFixture]
public sealed class AnimatorPreviewModeTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiTabSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorChainInspectorSystem());
        Root.Add(new AnimatorPreviewModeSystem());
    }

    private static EditorShellRects Shell() =>
        EditorShell.Compute(Screen, new EditorShellOptions(EditorFooterHeight.Animator));

    private static bool Shown(Entity entity) => !entity.Tags.Has<LayoutIgnore>();

    private void Toggle()
    {
        Click(Ui.PreviewButton);
        Frame(3);
    }

    [Test]
    public void PreviewHidesTheEditingPanelsAndShowsTheBar()
    {
        Toggle();

        Assert.That(Shown(Ui.ToolStrip), Is.False);
        Assert.That(Shown(Ui.RightPanel), Is.False);
        Assert.That(Shown(Ui.Timeline), Is.False);
        Assert.That(Shown(Ui.PreviewBar), Is.True);
        Assert.That(Ui.Inspector.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
        Assert.That(Ui.PreviewButton.GetComponent<Text>().Value, Is.EqualTo("EXIT PREVIEW"));
    }

    [Test]
    public void ThePreviewTakesTheWholeWidthAndTheTimelinesRoom()
    {
        Rect2 before = AnimatorGeometry.PanelArea(Ui.Preview);

        Toggle();
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);
        Rect2 bar = AnimatorGeometry.PanelArea(Ui.PreviewBar);

        Assert.That(preview.Size.X, Is.EqualTo(Screen.X).Within(0.5f));
        Assert.That(preview.Position.X, Is.EqualTo(0f).Within(0.5f));
        Assert.That(preview.Size.Y, Is.GreaterThan(before.Size.Y));
        Assert.That(bar.End.Y, Is.EqualTo(Screen.Y).Within(0.5f));
        Assert.That(bar.Position.Y, Is.GreaterThanOrEqualTo(preview.End.Y - 0.5f));
    }

    [Test]
    public void LeavingRestoresTheShellAndTheBoneDrawing()
    {
        Rect2 before = AnimatorGeometry.PanelArea(Ui.Preview);
        Toggle();
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.False);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.ExitPreviewMode));
        Frame(3);

        Rect2 after = AnimatorGeometry.PanelArea(Ui.Preview);
        Assert.That(Shown(Ui.ToolStrip), Is.True);
        Assert.That(Shown(Ui.RightPanel), Is.True);
        Assert.That(Shown(Ui.Timeline), Is.True);
        Assert.That(Shown(Ui.PreviewBar), Is.False);
        Assert.That(after.Position, Is.EqualTo(before.Position));
        Assert.That(after.Size, Is.EqualTo(before.Size));
        Assert.That(after.Position.X, Is.EqualTo(Shell().Viewport.Position.X).Within(0.5f));
        Assert.That(Session.GetComponent<AnimatorModes>().Kites, Is.True);
        Assert.That(Ui.Inspector.GetComponent<Position>().Value,
            Is.EqualTo(AnimatorLayout.InspectorCenter(Screen)));
    }

    [Test]
    public void TheChromeTurnsVioletWhilePreviewing()
    {
        Toggle();
        Assert.That(Ui.Header.GetComponent<FillColor>().Value, Is.EqualTo(AnimatorLayout.PreviewChrome));

        Toggle();
        Assert.That(Ui.Header.GetComponent<FillColor>().Value, Is.EqualTo(UiTheme.Panel));
    }

    [Test]
    public void TabAndEscapeAreBound()
    {
        Assert.That(AnimatorBindings.All, Has.Some.Matches<AnimatorBindings.Binding>(
            b => b.Action.Kind == ToolActionKind.TogglePreviewMode && b.Key == Key.Tab));
        Assert.That(AnimatorBindings.All, Has.Some.Matches<AnimatorBindings.Binding>(
            b => b.Action.Kind == ToolActionKind.ExitPreviewMode && b.Key == Key.Escape));
    }
}
