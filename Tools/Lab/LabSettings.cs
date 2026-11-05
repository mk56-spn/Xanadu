using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Lab;

/// <summary>
/// The lab-wide engine settings: a panel over the side panel, opened from the tab
/// strip, so the stage keeps running while they change. Each setting is a dropdown
/// that reads the engine every frame, so it never disagrees with what is in effect.
/// They apply to the root viewport; a stage's own SubViewport keeps its own.
/// </summary>
public sealed class LabSettings
{
    private sealed record Setting(string Label, string[] Options, Func<int> Read, Action<int> Apply);

    private static readonly float RowWidth = 220 + UiMetrics.Space.Sm * 2 + UiMetrics.Control.Md * 2;

    private static readonly int[] MaxFpsSteps = [0, 30, 60, 120, 144, 240];
    private static readonly Viewport.DebugDrawEnum[] DebugDraws =
        [Viewport.DebugDrawEnum.Disabled, Viewport.DebugDrawEnum.Unshaded, Viewport.DebugDrawEnum.Overdraw, Viewport.DebugDrawEnum.Wireframe];
    private static readonly DisplayServer.WindowMode[] WindowModes =
        [DisplayServer.WindowMode.Windowed, DisplayServer.WindowMode.Maximized, DisplayServer.WindowMode.Fullscreen, DisplayServer.WindowMode.ExclusiveFullscreen];

    private readonly Viewport _viewport;
    private readonly UiNode _toggle;
    private readonly UiNode _panel;
    private readonly (Setting Setting, UiDropdownNode Dropdown)[] _rows;

    /// <summary>Per row, the value last shown: a dropdown pick differs from it, and so does an outside change.</summary>
    private readonly int[] _seen;
    private bool _toggleHeld;
    private bool _open;

    public LabSettings(EntityStore store, Viewport viewport, Rect2 body, UiNode toggle)
    {
        _viewport = viewport;
        _toggle = toggle;
        ApplyArguments();

        UiNode layer = UiNode.Layer(store, default, UiLayers.LabSettings, null, UiStage.Full);
        _panel = layer.Panel().Size(body.Size).At(body.GetCenter()).Hidden();
        _panel.Text("ENGINE SETTINGS", UiType.Section, Palette.Accent);
        _panel.Text($"{RenderingServer.GetVideoAdapterName()} · {RenderingServer.GetCurrentRenderingMethod()}",
            UiType.Small, Palette.TextMuted);
        _panel.Text("F1 frame rate · F3 ECS and systems · F4 UI tree", UiType.Small, Palette.TextMuted);

        var rows = new System.Collections.Generic.List<(Setting, UiDropdownNode)>();
        foreach ((string heading, Setting[] settings) in Groups())
        {
            _panel.Text(heading, UiType.Label, Palette.Accent).Width(RowWidth).Align(TextAlign.Center);
            foreach (Setting setting in settings)
            {
                int selected = setting.Read();
                rows.Add((setting, _panel.Dropdown(Text(setting, selected), setting.Options, selected,
                    size: new Vector2(RowWidth, UiTheme.ButtonHeight))));
            }
        }
        _rows = rows.ToArray();
        _seen = Array.ConvertAll(_rows, row => row.Setting.Read());
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--settings") >= 0)
            SetOpen(true);
    }

    private (string Heading, Setting[] Settings)[] Groups() =>
    [
        ("RENDERING",
        [
            new("MSAA 2D", ["OFF", "2X", "4X", "8X"],
                () => (int)_viewport.Msaa2D, i => _viewport.Msaa2D = (Viewport.Msaa)i),
            new("HDR 2D", ["OFF", "ON"],
                () => _viewport.UseHdr2D ? 1 : 0, i => _viewport.UseHdr2D = i == 1),
            // Indexes line up with the enum: Nearest, Linear, LinearWithMipmaps, NearestWithMipmaps.
            new("FILTER", ["NEAREST", "LINEAR", "LINEAR MIP", "NEAREST MIP"],
                () => (int)_viewport.CanvasItemDefaultTextureFilter,
                i => _viewport.CanvasItemDefaultTextureFilter = (Viewport.DefaultCanvasItemTextureFilter)i),
            new("SNAP XFORMS", ["OFF", "ON"],
                () => _viewport.Snap2DTransformsToPixel ? 1 : 0, i => _viewport.Snap2DTransformsToPixel = i == 1),
            new("SNAP VERTS", ["OFF", "ON"],
                () => _viewport.Snap2DVerticesToPixel ? 1 : 0, i => _viewport.Snap2DVerticesToPixel = i == 1),
            new("DEBUG DRAW", ["OFF", "UNSHADED", "OVERDRAW", "WIREFRAME"],
                () => Math.Max(0, Array.IndexOf(DebugDraws, _viewport.DebugDraw)), i => _viewport.DebugDraw = DebugDraws[i]),
        ]),
        ("TIMING",
        [
            // Indexes line up with the enum: Disabled, Enabled, Adaptive, Mailbox.
            new("VSYNC", ["OFF", "ON", "ADAPTIVE", "MAILBOX"],
                () => (int)DisplayServer.WindowGetVsyncMode(), i => DisplayServer.WindowSetVsyncMode((DisplayServer.VSyncMode)i)),
            new("MAX FPS", ["NONE", "30", "60", "120", "144", "240"],
                () => Math.Max(0, Array.IndexOf(MaxFpsSteps, Engine.MaxFps)), i => Engine.MaxFps = MaxFpsSteps[i]),
        ]),
        ("WINDOW",
        [
            new("MODE", ["WINDOWED", "MAXIMIZED", "FULLSCREEN", "EXCLUSIVE"],
                () => Math.Max(0, Array.IndexOf(WindowModes, DisplayServer.WindowGetMode())),
                i => DisplayServer.WindowSetMode(WindowModes[i])),
        ]),
    ];

    /// <summary>Opens or closes on a fresh press of the strip button; applies picks and follows the engine.</summary>
    public void Poll()
    {
        bool down = _toggle.Entity.Tags.Has<Pressed>();
        if (down && !_toggleHeld)
            SetOpen(!_open);
        _toggleHeld = down;
        if (!_open)
            return;

        for (int i = 0; i < _rows.Length; i++)
        {
            (Setting setting, UiDropdownNode node) = _rows[i];
            Entity button = node.Control.Button;
            if (button.IsNull || !button.HasComponent<Dropdown>())
                continue;

            // Some settings (window mode, vsync) take a frame to read back, so a pick is
            // applied once and remembered rather than checked against the engine at once.
            ref Dropdown dropdown = ref button.GetComponent<Dropdown>();
            if (dropdown.Selected != _seen[i])
            {
                setting.Apply(dropdown.Selected);
                _seen[i] = dropdown.Selected;
            }
            else if (setting.Read() is var engine && engine != _seen[i])
            {
                _seen[i] = engine;
                dropdown.Selected = engine;
            }
            node.Button.SetText(Text(setting, _seen[i]));
        }
    }

    private void SetOpen(bool open)
    {
        _open = open;
        _panel.Hidden(!open);
        _toggle.SetText(open ? "CLOSE" : "SETTINGS");
    }

    private static string Text(Setting setting, int selected) =>
        $"{setting.Label}  {setting.Options[Math.Clamp(selected, 0, setting.Options.Length - 1)]}";

    /// <summary><c>--msaa=0|2|4|8</c> starts with that 2D MSAA; <c>--settings</c> (read in the constructor) starts with the panel open.</summary>
    private void ApplyArguments()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--msaa=", StringComparison.Ordinal)
                && Array.IndexOf([0, 2, 4, 8], int.TryParse(arg["--msaa=".Length..], out int value) ? value : -1) is >= 0 and var index)
                _viewport.Msaa2D = (Viewport.Msaa)index;
        }
    }
}
