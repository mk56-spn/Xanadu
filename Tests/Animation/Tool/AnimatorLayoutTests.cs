using Godot;
using NUnit.Framework;
using Writer.Tools.Animator;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorLayoutTests : AnimatorToolTest
{
    [Test]
    public void PanelsFillTheMiddleBandBetweenTheSidebars()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);

        // Margin 16 + left bar 240 + gap 12; gap 12 + right bar 300 + margin 16.
        Assert.That(preview.Position.X, Is.EqualTo(268f).Within(0.5f));
        Assert.That(preview.End.X, Is.EqualTo(Screen.X - 328f).Within(0.5f));
        Assert.That(preview.Position.Y, Is.EqualTo(116f).Within(0.5f));
    }

    [Test]
    public void TimelineSitsBelowThePreviewAndInsideTheScreen()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);
        Rect2 timeline = AnimatorGeometry.PanelArea(Ui.Timeline);

        Assert.That(timeline.Position.Y, Is.GreaterThanOrEqualTo(preview.End.Y));
        Assert.That(timeline.End.Y, Is.LessThanOrEqualTo(Screen.Y));
        Assert.That(timeline.Position.X, Is.EqualTo(16f).Within(0.5f));
        Assert.That(timeline.Size.X, Is.EqualTo(Screen.X - 32f).Within(0.5f));
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
