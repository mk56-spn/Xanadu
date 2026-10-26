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
        // Margin 16 + header 88 + gap 12, then the 36 px mode bar and its 8 px gap.
        Assert.That(preview.Position.Y, Is.EqualTo(160f).Within(0.5f));
    }

    [Test]
    public void TheEditingModesSitInABarAboveThePreview()
    {
        Rect2 preview = AnimatorGeometry.PanelArea(Ui.Preview);
        Rect2 bar = AnimatorGeometry.PanelArea(ModeButton(AnimatorMode.AutoKey).Parent);

        Assert.That(bar.End.Y, Is.LessThanOrEqualTo(preview.Position.Y + 0.5f));
        Assert.That(bar.Position.X, Is.EqualTo(preview.Position.X).Within(0.5f));
        Assert.That(bar.Size.X, Is.EqualTo(preview.Size.X).Within(0.5f));

        foreach (AnimatorMode mode in new[]
                 {
                     AnimatorMode.AutoKey,
                     AnimatorMode.Onion,
                     AnimatorMode.Rest,
                     AnimatorMode.Svg,
                     AnimatorMode.Ik,
                 })
        {
            Rect2 button = AnimatorGeometry.PanelArea(ModeButton(mode));
            Assert.That(bar.Encloses(button.Grow(-0.5f)), Is.True);
            Assert.That(button.End.Y, Is.LessThanOrEqualTo(preview.Position.Y));
        }
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
