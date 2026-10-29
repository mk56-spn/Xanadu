using Godot;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using NUnit.Framework;
using Xanadu.Systems.Ui;
using Xanadu.Tools.Animator;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorLayoutTests : AnimatorToolTest
{
    protected override void AddSystems() => Root.Add(new UiTabSystem());

    private static EditorShellRects Shell() =>
        EditorShell.Compute(Screen, new EditorShellOptions(EditorFooterHeight.Animator));

    [Test]
    public void ThePreviewFillsTheShellsViewport()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);
        Rect2 viewport = Shell().Viewport;

        Assert.That(preview.Position.X, Is.EqualTo(viewport.Position.X).Within(0.5f));
        Assert.That(preview.Position.Y, Is.EqualTo(viewport.Position.Y).Within(0.5f));
        Assert.That(preview.Size.X, Is.EqualTo(viewport.Size.X).Within(0.5f));
        Assert.That(preview.Size.Y, Is.EqualTo(viewport.Size.Y).Within(0.5f));
    }

    [Test]
    public void StatusAndDirtyFlagSitOnTheStatusLineUnderThePreview()
    {
        Rect2 line = Shell().StatusLine;
        Rect2 status = AnimatorGeometry.PanelArea(Ui.Status);
        Rect2 dirty = AnimatorGeometry.PanelArea(Ui.DirtyLabel);

        Assert.That(line.Encloses(status), Is.True);
        Assert.That(line.Encloses(dirty), Is.True);
        Assert.That(dirty.Position.X, Is.GreaterThan(status.Position.X));
    }

    [Test]
    public void TheTreeSitsOnTopOfTheRightPanelAboveTheTabs()
    {
        EditorShellRects shell = Shell();
        Rect2 bones = AnimatorGeometry.PanelArea(Ui.BoneList);
        Rect2 pose = AnimatorGeometry.PanelArea(TabButton(0));
        Rect2 edit = AnimatorGeometry.PanelArea(TabButton(1));

        Assert.That(shell.RightTree.Encloses(bones), Is.True);
        Assert.That(pose.Position.Y, Is.EqualTo(shell.RightTabs.Position.Y).Within(0.5f));
        Assert.That(pose.Position.X, Is.EqualTo(shell.RightTabs.Position.X).Within(0.5f));
        Assert.That(edit.Position.X, Is.EqualTo(pose.End.X).Within(0.5f));
        Assert.That(edit.End.X, Is.EqualTo(shell.RightTabs.End.X).Within(0.5f));
    }

    [Test]
    public void TheSelectedTabsBodyFillsTheBodySlotAndTheOtherIsParked()
    {
        EditorShellRects shell = Shell();
        Entity pose = Button(ToolActionKind.KeyBone).Parent.Parent;
        Entity edit = Button(ToolActionKind.FitPreview).Parent.Parent;

        Frame();

        Assert.That(AnimatorGeometry.PanelArea(pose).Position.X, Is.EqualTo(shell.RightBody.Position.X).Within(0.5f));
        Assert.That(AnimatorGeometry.PanelArea(pose).Position.Y, Is.EqualTo(shell.RightBody.Position.Y).Within(0.5f));
        Assert.That(edit.GetComponent<Xanadu.Ecs.Position>().Value, Is.EqualTo(UiLayout.Hidden));

        Click(TabButton(1));

        Assert.That(AnimatorGeometry.PanelArea(edit).Position.Y, Is.EqualTo(shell.RightBody.Position.Y).Within(0.5f));
        Assert.That(pose.GetComponent<Xanadu.Ecs.Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    private Entity TabButton(int index) =>
        Store.Query<UiTabs>().ToEntityList()[0].GetComponent<UiTabs>().Buttons[index];

    [Test]
    public void TheEditingModesRunDownTheToolStrip()
    {
        Rect2 strip = AnimatorGeometry.PanelArea(ModeButton(AnimatorMode.AutoKey).Parent);
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);

        Assert.That(strip.Position.X, Is.EqualTo(0f).Within(0.5f));
        Assert.That(strip.Size.X, Is.EqualTo(EditorShellMetrics.ToolStripWidth).Within(0.5f));
        Assert.That(strip.End.X, Is.LessThanOrEqualTo(preview.Position.X + 0.5f));

        float previous = float.MinValue;
        foreach (AnimatorMode mode in new[]
                 {
                     AnimatorMode.AutoKey,
                     AnimatorMode.Onion,
                     AnimatorMode.Rest,
                     AnimatorMode.Svg,
                     AnimatorMode.Ik,
                     AnimatorMode.Pin,
                     AnimatorMode.Kites,
                     AnimatorMode.Handles,
                 })
        {
            Rect2 button = AnimatorGeometry.PanelArea(ModeButton(mode));
            Assert.That(strip.Encloses(button.Grow(-0.5f)), Is.True);
            Assert.That(button.Size, Is.EqualTo(new Vector2(
                EditorShellMetrics.ToolButton, EditorShellMetrics.ToolButton)));
            Assert.That(button.Position.Y, Is.GreaterThan(previous));
            previous = button.Position.Y;
        }
    }

    [Test]
    public void TimelineSitsBelowThePreviewAndInsideTheScreen()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);
        Rect2 timeline = AnimatorGeometry.PanelArea(Ui.Timeline);

        Assert.That(timeline.Position.Y, Is.GreaterThanOrEqualTo(preview.End.Y));
        Assert.That(timeline.End.Y, Is.LessThanOrEqualTo(Screen.Y));
        Assert.That(timeline.Position.X, Is.EqualTo(0f).Within(0.5f));
        Assert.That(timeline.Size.X, Is.EqualTo(Screen.X).Within(0.5f));
        Assert.That(timeline.Size.Y, Is.EqualTo(EditorFooterHeight.Animator).Within(0.5f));
    }

    [Test]
    public void PlayAndStopSitAtTheLeftOfTheTimelineStripAndLoopStaysAtItsRight()
    {
        Rect2 panel = AnimatorGeometry.PanelArea(Ui.Timeline);
        Rect2 play = AnimatorGeometry.PanelArea(Button(ToolActionKind.Play));
        Rect2 stop = AnimatorGeometry.PanelArea(Button(ToolActionKind.Stop));
        Rect2 loop = AnimatorGeometry.PanelArea(Ui.LoopButton);

        Assert.That(panel.Encloses(play), Is.True);
        Assert.That(play.Position.Y, Is.LessThan(panel.Position.Y + 40f));
        Assert.That(stop.Position.X, Is.GreaterThan(play.End.X));
        Assert.That(stop.End.X, Is.LessThanOrEqualTo(
            panel.Position.X + 10f + AnimatorTimelineModel.TransportWidth + 0.5f));
        Assert.That(loop.Position.X, Is.GreaterThan(stop.End.X));
        Assert.That(panel.Encloses(loop), Is.True);
        Assert.That(loop.Position.Y, Is.EqualTo(play.Position.Y).Within(0.5f));
    }

    [Test]
    public void TimelineRulerRowsAndLabelsLieInsideItsPanel()
    {
        Rect2 panel = AnimatorGeometry.PanelArea(Ui.Timeline);
        AnimatorTimelineModel model = Model();

        Assert.That(panel.Encloses(model.Ruler), Is.True);
        Assert.That(panel.Encloses(model.Rows), Is.True);
        Assert.That(panel.Encloses(model.Labels), Is.True);
        Assert.That(model.Ruler.Size.X, Is.GreaterThan(1000f));
        Assert.That(model.Labels.End.X, Is.LessThanOrEqualTo(model.Rows.Position.X));
        Assert.That(model.Rows.Position.Y, Is.GreaterThanOrEqualTo(model.Ruler.End.Y));
        Assert.That(model.VisibleRows, Is.GreaterThanOrEqualTo(6));
    }
}
