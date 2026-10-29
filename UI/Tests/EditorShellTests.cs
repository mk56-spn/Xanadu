using Godot;
using NUnit.Framework;
using Xanadu.Ecs;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class EditorShellTests
{
    private static readonly Vector2 Screen = new(1920, 1080);

    [Test]
    public void RegionsTileTheScreenWithoutOverlap()
    {
        EditorShellRects rects = EditorShell.Compute(Screen, new EditorShellOptions());

        Assert.That(rects.Header, Is.EqualTo(new Rect2(0, 0, 1920, 80)));
        Assert.That(rects.Footer, Is.EqualTo(new Rect2(0, 880, 1920, 200)));
        Assert.That(rects.ToolStrip, Is.EqualTo(new Rect2(0, 80, 64, 800)));
        Assert.That(rects.RightPanel.End.X, Is.EqualTo(1920f));
        Assert.That(rects.Viewport.Position.X, Is.EqualTo(rects.ToolStrip.End.X));
        Assert.That(rects.Viewport.End.X, Is.EqualTo(rects.RightPanel.Position.X));
        Assert.That(rects.Viewport.Size.Y, Is.EqualTo(rects.Footer.Position.Y - rects.Header.End.Y));
    }

    [Test]
    public void OmittedPanelsGiveTheirSpaceToTheViewport()
    {
        EditorShellRects rects = EditorShell.Compute(
            Screen, new EditorShellOptions(ToolStrip: false, RightPanel: false));

        Assert.That(rects.ToolStrip.Size.X, Is.Zero);
        Assert.That(rects.RightPanel.Size.X, Is.Zero);
        Assert.That(rects.Viewport.Size.X, Is.EqualTo(1920f));
    }

    [Test]
    public void FooterHeightIsTheOneThingAToolMayResize()
    {
        EditorShellRects rects = EditorShell.Compute(
            Screen, new EditorShellOptions(FooterHeight: 300));

        Assert.That(rects.Footer.Size.Y, Is.EqualTo(300f));
        Assert.That(rects.Footer.End.Y, Is.EqualTo(1080f));
        Assert.That(rects.Viewport.Size.Y, Is.EqualTo(700f));
    }

    [Test]
    public void TabsShareTheStripAndSitOnTopOfTheBody()
    {
        EditorShellRects rects = EditorShell.Compute(Screen, new EditorShellOptions());

        Rect2 first = EditorShell.Tab(rects, 0, 2);
        Rect2 second = EditorShell.Tab(rects, 1, 2);

        Assert.That(first.End.X, Is.EqualTo(second.Position.X));
        Assert.That(second.End.X, Is.EqualTo(rects.RightPanel.End.X));
        Assert.That(rects.RightBody.Position.Y, Is.EqualTo(rects.RightTabs.End.Y));
    }

    [Test]
    public void ToolButtonsStackDownTheStripAndCapacityMatches()
    {
        EditorShellRects rects = EditorShell.Compute(Screen, new EditorShellOptions());
        int capacity = EditorShell.ToolButtonCapacity(rects);

        Vector2 last = EditorShell.ToolButtonPosition(rects, capacity - 1);

        Assert.That(capacity, Is.EqualTo(13));
        Assert.That(last.Y + EditorShellMetrics.ToolButton, Is.LessThanOrEqualTo(rects.ToolStrip.End.Y));
        Assert.That(EditorShell.ToolButtonPosition(rects, 0).X, Is.EqualTo(4f));
    }

    [Test]
    public void TinyScreensNeverGoNegative()
    {
        EditorShellRects rects = EditorShell.Compute(new Vector2(200, 100), new EditorShellOptions());

        Assert.That(rects.Viewport.Size.X, Is.GreaterThanOrEqualTo(0f));
        Assert.That(rects.Viewport.Size.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(rects.Footer.Size.Y, Is.GreaterThanOrEqualTo(0f));
    }
}
