namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// The fixed measurements every editor shares, so the header, tool strip,
/// inspector and footer sit in the same place in each tool.
/// </summary>
public static class EditorShellMetrics
{
    public const float HeaderHeight = 44f;
    public const float ToolStripWidth = 64f;
    public const float RightPanelWidth = 276f;

    /// <summary>Square tool buttons in the strip, and the gap between them.</summary>
    public const float ToolButton = 52f;
    public const float ToolGap = 6f;

    /// <summary>Height of the tab strip above the properties, under the tree.</summary>
    public const float TabHeight = 28f;

    /// <summary>The tree's share of the right panel's height; properties get the rest.</summary>
    public const float TreeShare = 0.4f;

    /// <summary>The strip along the viewport's bottom edge for playhead, zoom and the like.</summary>
    public const float StatusLineHeight = 24f;

    /// <summary>Header control height, centred in the header.</summary>
    public const float HeaderControlHeight = 30f;
    public const float HeaderPad = 7f;
    public const float HeaderGap = 12f;

    /// <summary>The editor name box at the header's left.</summary>
    public const float NameWidth = 250f;

    /// <summary>Header dropdown buttons, from the right edge: settings, then the two menus.</summary>
    public const float DropdownWidth = 180f;
    public const float SettingsWidth = 110f;
}

/// <summary>
/// The footer heights the tools choose from. A tool picks one when it builds
/// its shell; it is fixed for the session, never dragged.
/// </summary>
public static class EditorFooterHeight
{
    /// <summary>Keyframe rows under a preview: enough for a clip's tracks.</summary>
    public const float Animator = 216f;

    /// <summary>
    /// The level editor's transport row, the picked group's key strip and the timeline: the playfield
    /// above is the main work area, so the footer takes only what they need.
    /// </summary>
    public const float Level = 186f;
}

/// <summary>
/// The few things a tool may change about the shell, each for a logical
/// reason. The footer height has no default: every tool must pick one.
/// <see cref="PropertiesLeft"/> moves the tabs and properties out from under the
/// tree into a panel of their own beside the tool strip, so a tool whose tree
/// is long (the animator's rig) gives it the whole right panel.
/// </summary>
public sealed record EditorShellOptions(
    float FooterHeight,
    bool ToolStrip = true,
    bool RightPanel = true,
    bool PropertiesLeft = false);

/// <summary>
/// Where each region of the shell sits, in screen pixels. The tree is in the
/// right panel; the tabs and the properties under them are too, unless the
/// options put them in the left panel (otherwise empty, at the strip's edge).
/// </summary>
public readonly record struct EditorShellRects(
    Rect2 Header,
    Rect2 ToolStrip,
    Rect2 LeftPanel,
    Rect2 Viewport,
    Rect2 StatusLine,
    Rect2 RightPanel,
    Rect2 Tree,
    Rect2 Tabs,
    Rect2 Properties,
    Rect2 Footer);

/// <summary>The shell's geometry, shared by the builder, the systems and the tests.</summary>
public static class EditorShell
{
    public static EditorShellRects Compute(Vector2 screen, EditorShellOptions options)
    {
        float header = EditorShellMetrics.HeaderHeight;
        float footer = Math.Min(options.FooterHeight, Math.Max(0f, screen.Y - header));
        float strip = options.ToolStrip ? EditorShellMetrics.ToolStripWidth : 0f;
        float right = options.RightPanel ? EditorShellMetrics.RightPanelWidth : 0f;
        bool split = options.RightPanel && options.PropertiesLeft;
        float left = split ? EditorShellMetrics.RightPanelWidth : 0f;
        float middle = Math.Max(0f, screen.Y - header - footer);
        float viewportX = strip + left;
        float viewportWidth = Math.Max(0f, screen.X - viewportX - right);
        float status = Math.Min(EditorShellMetrics.StatusLineHeight, middle);
        float rightX = screen.X - right;

        // Split: the tree fills the right panel, the tabs and properties the left one.
        float tree = !options.RightPanel ? 0f : split ? middle : (float)Math.Floor(middle * EditorShellMetrics.TreeShare);
        float tabsTop = split ? header : header + tree;
        float tabsX = split ? strip : rightX;
        float room = split ? middle : middle - tree;
        float tabs = options.RightPanel ? Math.Min(EditorShellMetrics.TabHeight, room) : 0f;
        return new EditorShellRects(
            Header: new Rect2(0, 0, screen.X, header),
            ToolStrip: new Rect2(0, header, strip, middle),
            LeftPanel: new Rect2(strip, header, left, middle),
            Viewport: new Rect2(viewportX, header, viewportWidth, middle - status),
            StatusLine: new Rect2(viewportX, header + middle - status, viewportWidth, status),
            RightPanel: new Rect2(rightX, header, right, middle),
            Tree: new Rect2(rightX, header, right, tree),
            Tabs: new Rect2(tabsX, tabsTop, right, tabs),
            Properties: new Rect2(tabsX, tabsTop + tabs, right, Math.Max(0f, room - tabs)),
            Footer: new Rect2(0, header + middle, screen.X, footer));
    }

    /// <summary>Top-left of the nth tool button in the strip.</summary>
    public static Vector2 ToolButtonPosition(EditorShellRects rects, int index) =>
        rects.ToolStrip.Position + new Vector2(
            (rects.ToolStrip.Size.X - EditorShellMetrics.ToolButton) / 2,
            EditorShellMetrics.ToolGap +
            index * (EditorShellMetrics.ToolButton + EditorShellMetrics.ToolGap));

    /// <summary>How many tool buttons fit in the strip.</summary>
    public static int ToolButtonCapacity(EditorShellRects rects) =>
        (int)Math.Floor(
            (rects.ToolStrip.Size.Y - EditorShellMetrics.ToolGap) /
            (EditorShellMetrics.ToolButton + EditorShellMetrics.ToolGap));

    /// <summary>The nth of <paramref name="count"/> tabs, which share the strip equally.</summary>
    public static Rect2 Tab(EditorShellRects rects, int index, int count)
    {
        if (count <= 0)
            return default;

        float width = rects.Tabs.Size.X / count;
        return new Rect2(
            rects.Tabs.Position.X + index * width,
            rects.Tabs.Position.Y,
            width,
            rects.Tabs.Size.Y);
    }
}
