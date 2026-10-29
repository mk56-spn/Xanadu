using System;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// The fixed measurements every editor shares, so the header, tool strip,
/// inspector and footer sit in the same place in each tool.
/// </summary>
public static class EditorShellMetrics
{
    public const float HeaderHeight = 80f;
    public const float FooterHeight = 200f;
    public const float ToolStripWidth = 64f;
    public const float RightPanelWidth = 276f;

    /// <summary>Square tool buttons in the strip, and the gap between them.</summary>
    public const float ToolButton = 56f;
    public const float ToolGap = 3f;

    /// <summary>Height of the tab strip on top of the right panel.</summary>
    public const float TabHeight = 28f;

    /// <summary>Header control height, centred in the header.</summary>
    public const float HeaderControlHeight = 50f;
    public const float HeaderPad = 16f;
    public const float HeaderGap = 12f;

    /// <summary>The editor name box at the header's left.</summary>
    public const float NameWidth = 250f;

    /// <summary>The undo/redo block right of the name.</summary>
    public const float HistoryWidth = 224f;

    /// <summary>Header dropdown buttons, from the right edge: settings, then the two menus.</summary>
    public const float DropdownWidth = 180f;
    public const float SettingsWidth = 110f;
}

/// <summary>The few things a tool may change about the shell, each for a logical reason.</summary>
public sealed record EditorShellOptions(
    bool ToolStrip = true,
    bool RightPanel = true,
    float FooterHeight = EditorShellMetrics.FooterHeight);

/// <summary>Where each region of the shell sits, in screen pixels.</summary>
public readonly record struct EditorShellRects(
    Rect2 Header,
    Rect2 ToolStrip,
    Rect2 Viewport,
    Rect2 RightPanel,
    Rect2 RightTabs,
    Rect2 RightBody,
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
        float middle = Math.Max(0f, screen.Y - header - footer);
        float viewportWidth = Math.Max(0f, screen.X - strip - right);

        var rightPanel = new Rect2(screen.X - right, header, right, middle);
        float tabs = options.RightPanel ? EditorShellMetrics.TabHeight : 0f;
        return new EditorShellRects(
            Header: new Rect2(0, 0, screen.X, header),
            ToolStrip: new Rect2(0, header, strip, middle),
            Viewport: new Rect2(strip, header, viewportWidth, middle),
            RightPanel: rightPanel,
            RightTabs: new Rect2(rightPanel.Position, new Vector2(right, tabs)),
            RightBody: new Rect2(
                rightPanel.Position + new Vector2(0, tabs),
                new Vector2(right, Math.Max(0f, middle - tabs))),
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

        float width = rects.RightTabs.Size.X / count;
        return new Rect2(
            rects.RightTabs.Position.X + index * width,
            rects.RightTabs.Position.Y,
            width,
            rects.RightTabs.Size.Y);
    }
}
