using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;

namespace Xanadu.Lab.Experiments.Triangles;

/// <summary>
/// The side-panel half of a triangle benchmark: the grid-density stepper and the
/// measured frame times. The density is shared by every bench, so switching tabs
/// compares the two techniques on the same grid.
/// </summary>
public sealed class TriangleBench
{
    private static readonly float[] Cells = [64, 48, 32, 24, 16, 12, 8, 6, 4];
    private static readonly int[] LayerSteps = [1, 2, 4, 8, 16, 32, 64];
    private static int _cellIndex = StartIndex("--cell=", Cells, 2);
    private static int _layerIndex = StartIndex("--layers=", Array.ConvertAll(LayerSteps, l => (float)l), 0);

    /// <summary>Set by <c>--print-stats</c>: every report also goes to the console.</summary>
    private static readonly bool PrintStats = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--print-stats");

    private const float ReportSeconds = 0.5f;
    private const float StatsWidth = 580;

    private UiNode _cellLabel;
    private UiNode _layerLabel;
    private UiNode _stats;
    private Entity _minus;
    private Entity _plus;
    private Entity _fewer;
    private Entity _more;
    private bool _minusHeld;
    private bool _plusHeld;
    private bool _fewerHeld;
    private bool _moreHeld;

    private double _gpu;
    private double _cpu;
    private int _frames;
    private float _elapsed;

    /// <summary>Grid pitch in canvas pixels, shared by both tabs.</summary>
    public static float Cell => Cells[_cellIndex];

    /// <summary>How many times the field is drawn over itself, to lift its cost above frame noise.</summary>
    public static int Layers => LayerSteps[_layerIndex];

    /// <summary>Panel height the controls take, so the source listing can leave room.</summary>
    public static float Height =>
        UiTheme.ButtonHeight * 2 + UiNode.Measure("A", UiType.Small).Y * 2 + UiTheme.ContainerSpacing * 4;

    public void BuildPanel(UiNode panel)
    {
        _cellLabel = Stepper(panel, CellText(), out _minus, out _plus);
        _layerLabel = Stepper(panel, LayerText(), out _fewer, out _more);

        _stats = panel.Text("measuring...", UiType.Small, Palette.Accent).Width(StatsWidth);
        panel.Text("Whole-frame times. MSAA and the other engine settings are under SETTINGS.", UiType.Small, Palette.TextDim)
            .Width(StatsWidth);
    }

    /// <summary>Steps the density on a fresh press of - or +.</summary>
    public void Poll()
    {
        if (Pressed(_minus, ref _minusHeld))
            _cellIndex = Math.Max(0, _cellIndex - 1);
        if (Pressed(_plus, ref _plusHeld))
            _cellIndex = Math.Min(Cells.Length - 1, _cellIndex + 1);
        if (Pressed(_fewer, ref _fewerHeld))
            _layerIndex = Math.Max(0, _layerIndex - 1);
        if (Pressed(_more, ref _moreHeld))
            _layerIndex = Math.Min(LayerSteps.Length - 1, _layerIndex + 1);
        _cellLabel.SetText(CellText());
        _layerLabel.SetText(LayerText());
    }

    /// <summary>Folds in this frame's measured times and shows the average every half second.</summary>
    public void Report(Rid viewport, int triangles, double delta)
    {
        _gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
        _cpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
        _frames++;
        _elapsed += (float)delta;
        if (_elapsed < ReportSeconds)
            return;

        string line = $"GPU {_gpu / _frames:0.000} ms   CPU {_cpu / _frames:0.000} ms   {triangles * Layers:N0} TRIANGLES";
        _stats.SetText(line);
        if (PrintStats)
            GD.Print($"[tris] cell {Cell:0} layers {Layers} msaa {((SceneTree)Engine.GetMainLoop()).Root.Msaa2D}  {line}");
        _gpu = _cpu = 0;
        _frames = 0;
        _elapsed = 0;
    }

    /// <summary>The block of whole cells inside <paramref name="area"/>, on the canvas-aligned grid both shaders use.</summary>
    public static Rect2 FullCells(Rect2 area, float cell)
    {
        Vector2 start = (area.Position / cell).Ceil() * cell;
        Vector2 end = (area.End / cell).Floor() * cell;
        return new Rect2(start, end - start);
    }

    private static string CellText() => $"CELL {Cell:0} PX";

    private static string LayerText() => $"LAYERS {Layers}";

    /// <summary>A label, then - and + buttons.</summary>
    internal static UiNode Stepper(UiNode panel, string label, out Entity minus, out Entity plus)
    {
        UiNode row = panel.Row().Gap(UiMetrics.Space.Sm);
        UiNode text = row.Text(label, UiType.Label, Palette.Text).Width(220);
        minus = row.Button("-", UiType.Label).Size(UiMetrics.Control.Md, UiMetrics.Control.Md);
        plus = row.Button("+", UiType.Label).Size(UiMetrics.Control.Md, UiMetrics.Control.Md);
        return text;
    }

    /// <summary><paramref name="flag"/>N starts on that step, if it is one; otherwise on <paramref name="fallback"/>.</summary>
    internal static int StartIndex(string flag, float[] steps, int fallback)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(flag, StringComparison.Ordinal)
                && float.TryParse(arg[flag.Length..], out float value)
                && Array.IndexOf(steps, value) is >= 0 and var index)
                return index;
        }
        return fallback;
    }

    internal static bool Pressed(Entity button, ref bool held)
    {
        bool down = !button.IsNull && button.Tags.Has<Pressed>();
        bool fresh = down && !held;
        held = down;
        return fresh;
    }
}
