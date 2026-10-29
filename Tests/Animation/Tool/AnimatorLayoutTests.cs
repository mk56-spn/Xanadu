using Godot;
using Xanadu.Ecs;
using NUnit.Framework;
using Xanadu.Tools.Animator;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorLayoutTests : AnimatorToolTest
{
    [Test]
    public void PanelsFillTheMiddleBandBetweenTheSidebars()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);

        // Tool strip 64 + gap 12 + left bar 240 + gap 12; gap 12 + right bar 300; flush to the screen.
        Assert.That(preview.Position.X, Is.EqualTo(328f).Within(0.5f));
        Assert.That(preview.End.X, Is.EqualTo(Screen.X - 312f).Within(0.5f));
        // The shell's 80 px header + gap 12.
        Assert.That(preview.Position.Y, Is.EqualTo(92f).Within(0.5f));
    }

    [Test]
    public void TheEditingModesRunDownTheToolStrip()
    {
        Rect2 strip = AnimatorGeometry.PanelArea(ModeButton(AnimatorMode.AutoKey).Parent);
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);

        Assert.That(strip.Position.X, Is.EqualTo(0f).Within(0.5f));
        Assert.That(strip.Size.X, Is.EqualTo(EditorShellMetrics.ToolStripWidth).Within(0.5f));
        Assert.That(strip.End.X, Is.LessThan(preview.Position.X));

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
