using Godot;
using NUnit.Framework;
using Xanadu.Ecs;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class EditorShellTests
{
    private static readonly Vector2 Screen = new(1920, 1080);

    private static EditorShellRects Animator() =>
        EditorShell.Compute(Screen, new EditorShellOptions(EditorFooterHeight.Animator));

    [Test]
    public void RegionsTileTheScreenWithoutOverlap()
    {
        EditorShellRects rects = Animator();

        Assert.That(rects.Header, Is.EqualTo(new Rect2(0, 0, 1920, 44)));
        Assert.That(rects.Footer, Is.EqualTo(new Rect2(0, 864, 1920, 216)));
        Assert.That(rects.ToolStrip, Is.EqualTo(new Rect2(0, 44, 64, 820)));
        Assert.That(rects.RightPanel.End.X, Is.EqualTo(1920f));
        Assert.That(rects.Viewport.Position.X, Is.EqualTo(rects.ToolStrip.End.X));
        Assert.That(rects.Viewport.End.X, Is.EqualTo(rects.RightPanel.Position.X));
        Assert.That(rects.StatusLine.Position.Y, Is.EqualTo(rects.Viewport.End.Y));
        Assert.That(rects.StatusLine.End.Y, Is.EqualTo(rects.Footer.Position.Y));
    }

    [Test]
    public void ToolsPickDifferentFooterHeightsFromTheSharedValues()
    {
        EditorShellRects animator = Animator();
        EditorShellRects level = EditorShell.Compute(
            Screen, new EditorShellOptions(EditorFooterHeight.Level));

        Assert.That(EditorFooterHeight.Level, Is.Not.EqualTo(EditorFooterHeight.Animator));
        Assert.That(level.Footer.Size.Y, Is.EqualTo(EditorFooterHeight.Level));
        Assert.That(level.Footer.End.Y, Is.EqualTo(1080f));
        Assert.That(level.Header, Is.EqualTo(animator.Header));
        Assert.That(level.ToolStrip.Size.X, Is.EqualTo(animator.ToolStrip.Size.X));
        Assert.That(level.RightPanel.Size.X, Is.EqualTo(animator.RightPanel.Size.X));
    }

    [Test]
    public void OmittedPanelsGiveTheirSpaceToTheViewport()
    {
        EditorShellRects rects = EditorShell.Compute(
            Screen,
            new EditorShellOptions(EditorFooterHeight.Animator, ToolStrip: false, RightPanel: false));

        Assert.That(rects.ToolStrip.Size.X, Is.Zero);
        Assert.That(rects.RightPanel.Size.X, Is.Zero);
        Assert.That(rects.RightTree.Size.Y, Is.Zero);
        Assert.That(rects.RightTabs.Size.Y, Is.Zero);
        Assert.That(rects.Viewport.Size.X, Is.EqualTo(1920f));
    }

    [Test]
    public void RightPanelStacksTreeThenTabsThenProperties()
    {
        EditorShellRects rects = Animator();

        Assert.That(rects.RightTree.Position.Y, Is.EqualTo(rects.RightPanel.Position.Y));
        Assert.That(rects.RightTabs.Position.Y, Is.EqualTo(rects.RightTree.End.Y));
        Assert.That(rects.RightBody.Position.Y, Is.EqualTo(rects.RightTabs.End.Y));
        Assert.That(rects.RightBody.End.Y, Is.EqualTo(rects.RightPanel.End.Y));
        Assert.That(rects.RightTree.Size.Y, Is.EqualTo(328f));
    }

    [Test]
    public void TabsShareTheStrip()
    {
        EditorShellRects rects = Animator();

        Rect2 first = EditorShell.Tab(rects, 0, 2);
        Rect2 second = EditorShell.Tab(rects, 1, 2);

        Assert.That(first.End.X, Is.EqualTo(second.Position.X));
        Assert.That(second.End.X, Is.EqualTo(rects.RightPanel.End.X));
    }

    [Test]
    public void ToolButtonsStackDownTheStripAndCapacityMatches()
    {
        EditorShellRects rects = Animator();
        int capacity = EditorShell.ToolButtonCapacity(rects);

        Vector2 last = EditorShell.ToolButtonPosition(rects, capacity - 1);

        Assert.That(capacity, Is.EqualTo(13));
        Assert.That(last.Y + EditorShellMetrics.ToolButton, Is.LessThanOrEqualTo(rects.ToolStrip.End.Y));
        Assert.That(EditorShell.ToolButtonPosition(rects, 0).X, Is.EqualTo(4f));
    }

    [Test]
    public void TinyScreensNeverGoNegative()
    {
        EditorShellRects rects = EditorShell.Compute(
            new Vector2(200, 100), new EditorShellOptions(EditorFooterHeight.Level));

        foreach (Rect2 rect in new[]
                 {
                     rects.Viewport, rects.StatusLine, rects.Footer, rects.RightTree,
                     rects.RightTabs, rects.RightBody, rects.ToolStrip,
                 })
        {
            Assert.That(rect.Size.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rect.Size.Y, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
