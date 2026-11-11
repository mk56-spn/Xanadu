using System;
using System.Collections.Generic;
using System.Globalization;
using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Tools.DevTools;
using Xanadu.Host.Autopsy;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;

namespace Xanadu.Tools.Autopsy;

/// <summary>
/// Autopsy of a frame: opens the capture each exe saved when its last launch
/// closed (HostCore/Autopsy) and takes one frame apart. A strip of the last
/// seconds' frames (CPU split into update, draw and idle; GPU below) picks the
/// frame; the mean frame and the launch's worst frames are a click away. Two
/// split bars then show where that frame's CPU and GPU time went against the
/// display's budget; click a segment to open it, a crumb to go back out.
/// </summary>
internal sealed class AutopsyStage : CanvasToolStage
{
    private const float Margin = 40;
    private const float Left = 150;
    private const float Right = 1880;
    private const float StripTop = 150, LaneHeight = 84, LaneGap = 12;
    private const float PickY = 352;
    private const float BarsTop = 410, BarHeight = 58, BarBlock = 132;
    private const float TableTop = 690, RowHeight = 26;
    private const int MaxRows = 11;

    private static readonly Color[] Segments =
    [
        Palette.InfoSoft, Palette.Violet, Palette.Pink, Palette.Lime, Palette.Warning, Palette.Cyan,
        Palette.Gold, Palette.Success, Palette.PrimarySoft, Palette.Danger,
    ];

    private static readonly Color Self = new(0.36f, 0.4f, 0.48f);
    private static readonly Color Idle = new(0.22f, 0.26f, 0.33f);

    private readonly List<AutopsyCapture> _captures;
    private int _captureIndex = -1;
    private FrameAutopsy? _autopsy;
    private string? _error;
    private List<int>[] _children = [];
    private readonly List<AutopsyFrame> _recent = new(), _worstCpu = new(), _worstGpu = new();
    private AutopsyFrame? _mean, _frame;
    private int _worstCpuIndex = -1, _worstGpuIndex = -1;
    private readonly int[] _focus = [FrameAutopsy.CpuRoot, FrameAutopsy.GpuRoot];
    private int _update = -1, _draw = -1, _idle = -1;

    // The METHODS view: the launch's CPU samples, read in the background.
    private bool _methodsView;
    private bool _wholeLaunch = true;
    private MethodProfile? _profile;
    private System.Threading.Tasks.Task<MethodProfile>? _profileTask;
    private string? _profileError;

    /// <summary>Where a system's track can open in Atlas; null without the chart.</summary>
    private readonly ICodeLinks? _links;

    /// <summary>Each CPU track's type, once worked out for the open capture (null: not a system or group).</summary>
    private readonly Dictionary<int, CodeLink?> _types = new();

    // Pointer, and what this frame's drawing found under it.
    private Vector2 _mouse;
    private bool _click;
    private bool _wasDown;
    private string? _hint;
    private Action? _onClick;

    public AutopsyStage(DevToolsContext context, Handle item, Rect2 area, ICodeLinks? links = null, bool methods = false) : base(context, item, area)
    {
        _links = links;
        _methodsView = methods;
        _captures = AutopsyStore.List();
        if (_captures.Count > 0)
            Open(0);
    }

    // ---- data ---------------------------------------------------------------------

    private void Open(int index)
    {
        _captureIndex = index;
        _error = null;
        _types.Clear();
        try
        {
            _autopsy = AutopsyStore.Load(_captures[index]);
        }
        catch (Exception exception) when (exception is FormatException or System.IO.IOException)
        {
            _autopsy = null;
            _error = exception.Message;
            return;
        }

        _children = new List<int>[_autopsy.Tracks.Count];
        for (int i = 0; i < _children.Length; i++)
            _children[i] = new List<int>();
        for (int i = 0; i < _autopsy.Tracks.Count; i++)
        {
            int parent = _autopsy.Tracks[i].Parent;
            if (parent >= 0)
                _children[parent].Add(i);
        }
        _update = Find(FrameAutopsy.CpuRoot, "update");
        _draw = Find(FrameAutopsy.CpuRoot, "draw");
        _idle = Find(FrameAutopsy.CpuRoot, "idle (pacing, vsync)");

        _recent.Clear();
        _worstCpu.Clear();
        _worstGpu.Clear();
        _mean = null;
        foreach (AutopsyFrame frame in _autopsy.Frames)
        {
            if (frame.Keep.HasFlag(AutopsyKeep.Mean))
                _mean = frame;
            if (frame.Keep.HasFlag(AutopsyKeep.Recent))
                _recent.Add(frame);
            if (frame.Keep.HasFlag(AutopsyKeep.WorstCpu))
                _worstCpu.Add(frame);
            if (frame.Keep.HasFlag(AutopsyKeep.WorstGpu))
                _worstGpu.Add(frame);
        }
        _worstCpu.Sort((a, b) => b.Get(FrameAutopsy.CpuRoot).CompareTo(a.Get(FrameAutopsy.CpuRoot)));
        _worstGpu.Sort((a, b) => b.Get(FrameAutopsy.GpuRoot).CompareTo(a.Get(FrameAutopsy.GpuRoot)));
        _worstCpuIndex = _worstGpuIndex = -1;
        _focus[0] = FrameAutopsy.CpuRoot;
        _focus[1] = FrameAutopsy.GpuRoot;
        _frame = _mean ?? (_recent.Count > 0 ? _recent[^1] : null);

        // TraceEvent takes a few seconds on a long trace, so read it off the frame loop.
        _profile = null;
        _profileError = null;
        AutopsyCapture capture = _captures[index];
        long mainThread = _autopsy.MainThread;
        _profileTask = capture.HasTrace ? System.Threading.Tasks.Task.Run(() => MethodProfile.Read(capture, mainThread)) : null;
    }

    private int Find(int parent, string name)
    {
        int at = _children[parent].FindIndex(child => _autopsy!.Tracks[child].Name == name);
        return at < 0 ? -1 : _children[parent][at];
    }

    /// <summary>A track's milliseconds in a frame; 0 where it didn't run.</summary>
    private static float Ms(AutopsyFrame frame, int track)
    {
        if (track < 0)
            return 0;
        float value = frame.Get(track);
        return float.IsNaN(value) ? 0 : value;
    }

    // ---- frame --------------------------------------------------------------------

    protected override void Tick(float delta, float time)
    {
        _mouse = Mouse;
        bool down = LeftDown;
        _click = down && !_wasDown;
        _wasDown = down;
        _hint = null;
        _onClick = null;

        Canvas.Clear(Item);
        Canvas.DrawRect(Item, new Rect2(0, 0, Width, Height), Palette.Background);
        DrawHeader();
        if (_autopsy is not null && _frame is not null)
        {
            DrawStrip();
            DrawPicks();
            if (_methodsView)
                DrawMethods();
            else
            {
                DrawBar(0, BarsTop);
                DrawBar(1, BarsTop + BarBlock);
                DrawTable(0, Margin);
                DrawTable(1, Width / 2f + Margin / 2);
            }
        }
        else
        {
            string message = _error ?? "No captures yet: run the game, a lab or the animator and close it; its last launch shows up here.";
            Label(new Rect2(Margin, 300, Width - Margin * 2, 40), message, Palette.TextMuted, 22, center: true);
        }

        if (_hint is not null)
            DrawHint(_hint);

        // A press runs whatever this frame's drawing found under the pointer.
        if (_click)
            _onClick?.Invoke();
    }

    private void DrawHeader()
    {
        Label(new Rect2(Margin, 34, 600, 44), "AUTOPSY OF A FRAME", Palette.Accent, 34);
        if (_autopsy is not null)
        {
            Button(new Rect2(470, 36, 140, 40), "FRAME", !_methodsView, () => _methodsView = false, "CPU and GPU time split into systems and passes");
            Button(new Rect2(616, 36, 140, 40), "METHODS", _methodsView, () => _methodsView = true,
                "Where the frame thread's time went, method by method (launches run with XANADU_PROFILE=1)");
        }
        float x = Right;
        for (int i = _captures.Count - 1; i >= 0; i--)
        {
            AutopsyCapture capture = _captures[i];
            string label = $"{capture.App.ToUpperInvariant()}  {capture.Saved:HH:mm}";
            var box = new Rect2(x - 220, 36, 220, 40);
            int index = i;
            Button(box, label, i == _captureIndex, () => Open(index), $"Last {capture.App} launch, closed {capture.Saved:yyyy-MM-dd HH:mm:ss}");
            x -= 228;
        }

        if (_autopsy is null)
            return;
        string summary = $"{_autopsy.App}  ·  launched {_autopsy.Started:yyyy-MM-dd HH:mm:ss}  ·  ran {Duration(_autopsy.Seconds)}  ·  " +
                         $"{_autopsy.FrameCount:N0} frames  ·  budget {_autopsy.BudgetMs:0.0} ms ({1000 / _autopsy.BudgetMs:0} Hz)  ·  " +
                         $"strip shows the last {_recent.Count:N0}";
        Label(new Rect2(Margin, 92, Width - Margin * 2, 30), summary, Palette.TextDim, 18);
    }

    private static string Duration(double seconds) =>
        seconds < 60 ? $"{seconds:0.0} s" : $"{(int)(seconds / 60)} min {seconds % 60:00} s";

    /// <summary>The recent frames: CPU split into update, draw and idle above; GPU below; the budget as a line.</summary>
    private void DrawStrip()
    {
        float budget = _autopsy!.BudgetMs;
        float scale = StripScale(budget);
        var cpuLane = new Rect2(Left, StripTop, Right - Left, LaneHeight);
        var gpuLane = new Rect2(Left, StripTop + LaneHeight + LaneGap, Right - Left, LaneHeight);
        Label(new Rect2(Margin, cpuLane.Position.Y + 28, 100, 28), "CPU", Palette.TextMuted, 20);
        Label(new Rect2(Margin, gpuLane.Position.Y + 28, 100, 28), "GPU", Palette.TextMuted, 20);
        Canvas.DrawRect(Item, cpuLane, Palette.SurfaceDeep);
        Canvas.DrawRect(Item, gpuLane, Palette.SurfaceDeep);

        int count = Math.Max(1, _recent.Count);
        float step = cpuLane.Size.X / count;
        for (int i = 0; i < _recent.Count; i++)
        {
            AutopsyFrame frame = _recent[i];
            float x = cpuLane.Position.X + i * step;
            float width = MathF.Max(step - (step > 3 ? 1 : 0), 1);
            float bottom = cpuLane.End.Y;
            foreach ((int track, Color colour) in (ReadOnlySpan<(int, Color)>)[(_update, Segments[0]), (_draw, Segments[1]), (_idle, Idle)])
            {
                float h = MathF.Min(Ms(frame, track) / scale * LaneHeight, bottom - cpuLane.Position.Y);
                Canvas.DrawRect(Item, new Rect2(x, bottom - h, width, h), colour);
                bottom -= h;
            }
            if (Ms(frame, FrameAutopsy.CpuRoot) > scale)
                Canvas.DrawRect(Item, new Rect2(x, cpuLane.Position.Y, width, 3), Palette.Danger);

            float gpu = Ms(frame, FrameAutopsy.GpuRoot);
            float gh = MathF.Min(gpu / scale * LaneHeight, LaneHeight);
            Canvas.DrawRect(Item, new Rect2(x, gpuLane.End.Y - gh, width, gh), Palette.Cyan);
            if (gpu > scale)
                Canvas.DrawRect(Item, new Rect2(x, gpuLane.Position.Y, width, 3), Palette.Danger);

            if (frame == _frame)
            {
                Canvas.DrawRect(Item, new Rect2(x - 1, cpuLane.Position.Y - 6, MathF.Max(width, 2) + 2, 4), Palette.Accent);
                Canvas.DrawRect(Item, new Rect2(x - 1, gpuLane.End.Y + 2, MathF.Max(width, 2) + 2, 4), Palette.Accent);
            }

            var column = new Rect2(x, cpuLane.Position.Y, step, gpuLane.End.Y - cpuLane.Position.Y);
            if (column.HasPoint(_mouse))
            {
                Canvas.DrawRect(Item, new Rect2(x, cpuLane.Position.Y, MathF.Max(width, 1), column.Size.Y), new Color(1, 1, 1, 0.12f));
                _hint = $"frame {frame.Number}  ·  CPU {Ms(frame, FrameAutopsy.CpuRoot):0.00} ms (update {Ms(frame, _update):0.00}, draw {Ms(frame, _draw):0.00}, idle {Ms(frame, _idle):0.00})  ·  GPU {gpu:0.000} ms";
                AutopsyFrame picked = frame;
                Clickable(column, () => Pick(picked));
            }
        }

        foreach (Rect2 lane in (ReadOnlySpan<Rect2>)[cpuLane, gpuLane])
        {
            float y = lane.End.Y - budget / scale * LaneHeight;
            Canvas.DrawRect(Item, new Rect2(lane.Position.X, y, lane.Size.X, 1), Palette.Accent with { A = 0.6f });
            Label(new Rect2(Right + 2, y - 10, Width - Right - 2, 20), $"{budget:0.0}", Palette.Accent, 13);
        }
    }

    /// <summary>Tall enough for nineteen frames in twenty and the budget line, so one hitch doesn't flatten the rest.</summary>
    private float StripScale(float budget)
    {
        var tops = new List<float>(_recent.Count);
        foreach (AutopsyFrame frame in _recent)
            tops.Add(MathF.Max(Ms(frame, FrameAutopsy.CpuRoot), Ms(frame, FrameAutopsy.GpuRoot)));
        if (tops.Count == 0)
            return budget * 2;
        tops.Sort();
        return MathF.Max(budget * 1.5f, tops[(int)(tops.Count * 0.95f)] * 1.15f);
    }

    private void Pick(AutopsyFrame frame)
    {
        _frame = frame;
        // Keep the open groups when the new frame has them.
        for (int side = 0; side < 2; side++)
        {
            if (float.IsNaN(frame.Get(_focus[side])))
                _focus[side] = side == 0 ? FrameAutopsy.CpuRoot : FrameAutopsy.GpuRoot;
        }
    }

    private void DrawPicks()
    {
        float x = Margin;
        Button(new Rect2(x, PickY, 170, 38), "MEAN FRAME", _frame == _mean && _mean is not null,
            () => { if (_mean is not null) Pick(_mean); }, "Every track averaged over the whole launch");
        x += 178;
        Button(new Rect2(x, PickY, 130, 38), "LATEST", _recent.Count > 0 && _frame == _recent[^1],
            () => { if (_recent.Count > 0) Pick(_recent[^1]); }, "The last frame before the window closed");
        x += 150;
        x = Worst(x, "WORST CPU", cpu: true, "The launch's slowest frames on the CPU, slowest first");
        x += 20;
        Worst(x, "WORST GPU", cpu: false, "The launch's slowest frames on the GPU, slowest first");

        AutopsyFrame frame = _frame!;
        string which = frame == _mean ? "mean frame" : $"frame {frame.Number:N0}";
        string line = $"{which}   CPU {Ms(frame, FrameAutopsy.CpuRoot):0.00} ms   GPU {Ms(frame, FrameAutopsy.GpuRoot):0.000} ms";
        Label(new Rect2(1100, PickY + 4, Right - 1100, 32), line, Palette.TextBright, 22, right: true);
    }

    private float Worst(float x, string label, bool cpu, string tooltip)
    {
        List<AutopsyFrame> worst = cpu ? _worstCpu : _worstGpu;
        int index = cpu ? _worstCpuIndex : _worstGpuIndex;
        bool showing = index >= 0 && index < worst.Count && _frame == worst[index];
        string text = showing ? $"{label}  {index + 1}/{worst.Count}" : label;
        Button(new Rect2(x, PickY, 200, 38), text, showing, () => ShowWorst(cpu, 0), tooltip);
        Button(new Rect2(x + 204, PickY, 38, 38), "<", false, () => ShowWorst(cpu, index - 1), "Slower");
        Button(new Rect2(x + 246, PickY, 38, 38), ">", false, () => ShowWorst(cpu, index + 1), "Less slow");
        return x + 284;
    }

    private void ShowWorst(bool cpu, int index)
    {
        List<AutopsyFrame> worst = cpu ? _worstCpu : _worstGpu;
        if (worst.Count == 0)
            return;
        index = Math.Clamp(index, 0, worst.Count - 1);
        if (cpu)
            _worstCpuIndex = index;
        else
            _worstGpuIndex = index;
        Pick(worst[index]);
    }

    // ---- split bars ---------------------------------------------------------------

    /// <summary>
    /// The open group's children side by side, wide as their time. At the top
    /// the bar is measured against the budget (or the frame, if it ran over);
    /// inside a group it fills the width. Time a group spent outside its
    /// children shows as "(self)".
    /// </summary>
    private void DrawBar(int side, float top)
    {
        AutopsyFrame frame = _frame!;
        int root = side == 0 ? FrameAutopsy.CpuRoot : FrameAutopsy.GpuRoot;
        int focus = _focus[side];
        float total = Ms(frame, focus);
        bool atRoot = focus == root;
        float budget = _autopsy!.BudgetMs;
        float scale = atRoot ? MathF.Max(budget, total) : MathF.Max(total, 1e-6f);

        // Crumbs: SIDE › parent › … › open group.
        var path = new List<int>();
        for (int t = focus; t >= 0; t = _autopsy.Tracks[t].Parent)
            path.Insert(0, t);
        float x = Margin;
        Label(new Rect2(x, top, 100, 28), side == 0 ? "CPU" : "GPU", Palette.TextMuted, 20);
        x = Left;
        for (int i = 0; i < path.Count; i++)
        {
            int track = path[i];
            string name = i == 0 ? "frame" : _autopsy.Tracks[track].Name;
            float width = Measure(name, 18) + 16;
            var box = new Rect2(x, top, width, 28);
            bool last = i == path.Count - 1;
            if (!last && box.HasPoint(_mouse))
            {
                Canvas.DrawRect(Item, box, Palette.PrimaryMuted);
                Clickable(box, () => _focus[side] = track);
            }
            Label(box, name, last ? Palette.TextBright : Palette.InfoSoft, 18, center: true);
            x += width;
            if (!last)
            {
                Label(new Rect2(x, top, 20, 28), "›", Palette.TextDim, 18, center: true);
                x += 20;
            }
        }
        string of = atRoot ? $"{total:0.000} ms  ·  {total / budget * 100:0}% of budget" : $"{total:0.000} ms  ·  {total / Math.Max(Ms(frame, root), 1e-6f) * 100:0.0}% of the frame";
        Label(new Rect2(Right - 520, top, 520, 28), of, Palette.TextMuted, 18, right: true);

        var bar = new Rect2(Left, top + 36, Right - Left, BarHeight);
        Canvas.DrawRect(Item, bar, Palette.SurfaceDeep);
        float px = bar.Position.X;
        float children = 0;
        int colour = 0;
        foreach (int child in _children[focus])
        {
            float ms = Ms(frame, child);
            if (ms <= 0)
                continue;
            children += ms;
            Color c = child == _idle ? Idle : Segments[colour++ % Segments.Length];
            px = Segment(side, child, _autopsy.Tracks[child].Name, ms, px, bar, scale, total, c);
        }
        float self = total - children;
        if (self > total * 0.005f && self > 0.0005f)
            px = Segment(side, -1, "(self)", self, px, bar, scale, total, Self);

        if (atRoot)
        {
            float bx = bar.Position.X + budget / scale * bar.Size.X;
            Canvas.DrawRect(Item, new Rect2(bx - 1, bar.Position.Y - 6, 2, bar.Size.Y + 12), Palette.Accent);
            if (total < budget)
                Label(new Rect2(px + 8, bar.Position.Y, bx - px - 16, bar.Size.Y), $"{budget - total:0.00} ms to spare", Palette.TextDim, 16);
        }
    }

    private float Segment(int side, int track, string name, float ms, float x, Rect2 bar, float scale, float total, Color colour)
    {
        float width = ms / scale * bar.Size.X;
        var box = new Rect2(x, bar.Position.Y, MathF.Max(width, 1), bar.Size.Y);
        bool hover = box.HasPoint(_mouse);
        Canvas.DrawRect(Item, box, hover ? colour.Lerp(Colors.White, 0.25f) : colour);
        Canvas.DrawRect(Item, new Rect2(box.End.X - 1, box.Position.Y, 1, box.Size.Y), Palette.Background);
        bool opens = track >= 0 && HasTime(track);
        if (width > 60)
        {
            Color ink = new(0.05f, 0.07f, 0.12f);
            Label(new Rect2(x + 6, bar.Position.Y + 6, width - 12, 22), opens ? name + " ›" : name, ink, 16);
            Label(new Rect2(x + 6, bar.Position.Y + 30, width - 12, 22), $"{ms:0.00} ms", ink, 15);
        }
        if (hover)
        {
            _hint = $"{name}  ·  {ms:0.000} ms  ·  {ms / Math.Max(total, 1e-6f) * 100:0.0}% of this bar  ·  {ms / _autopsy!.BudgetMs * 100:0.0}% of budget" + (opens ? "  ·  click to open" : "");
            if (opens)
                Clickable(box, () => _focus[side] = track);
        }
        return x + width;
    }

    private bool HasTime(int track)
    {
        foreach (int child in _children[track])
        {
            if (Ms(_frame!, child) > 0)
                return true;
        }
        return false;
    }

    // ---- tables -------------------------------------------------------------------

    /// <summary>The open group's children and grandchildren, slowest first, as rows; a row with children opens it.</summary>
    private void DrawTable(int side, float left)
    {
        AutopsyFrame frame = _frame!;
        int focus = _focus[side];
        float width = Width / 2f - Margin * 1.5f;
        Label(new Rect2(left, TableTop, width, 28), side == 0 ? "CPU, slowest first" : "GPU, slowest first", Palette.TextMuted, 18);
        var rows = new List<(int Track, int Depth)>();
        Collect(focus, 0, rows);
        float total = MathF.Max(Ms(frame, focus), 1e-6f);
        float y = TableTop + 34;
        int shown = 0;
        foreach ((int track, int depth) in rows)
        {
            if (shown++ >= MaxRows)
            {
                Label(new Rect2(left, y, width, RowHeight), $"… {rows.Count - MaxRows} more", Palette.TextDim, 15);
                break;
            }
            float ms = Ms(frame, track);
            var row = new Rect2(left, y, width, RowHeight);
            bool opens = HasTime(track);
            if (row.HasPoint(_mouse) && opens)
            {
                Canvas.DrawRect(Item, row, Palette.PrimaryMuted);
                int open = track;
                Clickable(row, () => _focus[side] = open);
            }
            float indent = depth * 22;
            Canvas.DrawRect(Item, new Rect2(left + indent, y + 7, 12, 12), depth == 0 ? ColourOf(focus, track) : Palette.BorderSubtle);
            Label(new Rect2(left + indent + 20, y, width * 0.55f - indent, RowHeight), _autopsy!.Tracks[track].Name + (opens ? " ›" : ""),
                depth == 0 ? Palette.Text : Palette.TextDim, 16);
            if (row.HasPoint(_mouse) && TypeOf(track) is { } link)
            {
                // Added after the row's own click, so over the button this one wins.
                Button(new Rect2(left + width * 0.58f - 82, y + 2, 74, RowHeight - 4), "ATLAS", false,
                    () => _links!.Open(link.Type),
                    $"{link.Type} in Atlas: what it queries, uses and is used by");
            }
            float barLeft = left + width * 0.58f;
            float barWidth = width * 0.22f;
            Canvas.DrawRect(Item, new Rect2(barLeft, y + 8, barWidth, 10), Palette.SurfaceDeep);
            Canvas.DrawRect(Item, new Rect2(barLeft, y + 8, barWidth * MathF.Min(ms / total, 1), 10), depth == 0 ? Palette.InfoSoft : Palette.BorderStrong);
            Label(new Rect2(left + width * 0.8f, y, width * 0.2f, RowHeight), $"{ms:0.000} ms", Palette.TextMuted, 16, right: true);
            y += RowHeight;
        }
        if (rows.Count == 0)
            Label(new Rect2(left, y, width, RowHeight), "nothing inside this one", Palette.TextDim, 16);
    }

    /// <summary>
    /// The system or group a CPU track times, if Atlas knows it. A track named
    /// after a system is that system, and a group's track carries its short name
    /// (Editor → EditorSystemGroup). Inside a known group it has to be one the
    /// group uses, and tracks inside a system time parts of its work, not
    /// types, so they have none.
    /// </summary>
    private CodeLink? TypeOf(int track)
    {
        if (_links is not { Ready: true } links || track < 0)
            return null;
        if (_types.TryGetValue(track, out CodeLink? known))
            return known;

        CodeLink? link = null;
        int parent = _autopsy!.Tracks[track].Parent;
        CodeLink? owner = parent >= 0 ? TypeOf(parent) : null;
        if (UnderCpu(track) && owner is not { Group: false })
            link = links.Resolve(_autopsy.Tracks[track].Name, owner);
        _types[track] = link;
        return link;
    }

    private bool UnderCpu(int track)
    {
        for (int at = track; at >= 0; at = _autopsy!.Tracks[at].Parent)
        {
            if (at == FrameAutopsy.CpuRoot)
                return true;
        }

        return false;
    }

    private void Collect(int track, int depth, List<(int, int)> rows)
    {
        var children = new List<int>(_children[track]);
        children.RemoveAll(child => Ms(_frame!, child) <= 0);
        children.Sort((a, b) => Ms(_frame!, b).CompareTo(Ms(_frame!, a)));
        foreach (int child in children)
        {
            rows.Add((child, depth));
            if (depth == 0)
                Collect(child, depth + 1, rows);
        }
    }

    /// <summary>The colour a direct child of the open group has in its bar.</summary>
    private Color ColourOf(int focus, int track)
    {
        if (track == _idle)
            return Idle;
        int colour = 0;
        foreach (int child in _children[focus])
        {
            if (Ms(_frame!, child) <= 0)
                continue;
            if (child == track)
                return Segments[colour % Segments.Length];
            if (child != _idle)
                colour++;
        }
        return Palette.BorderSubtle;
    }

    // ---- methods ------------------------------------------------------------------

    private const float MethodsTop = 410;
    private const int MethodRows = 17;
    private const int BarMethods = 12;

    /// <summary>
    /// The frame thread's samples, for the whole launch or the picked frame:
    /// a split bar of the methods it was found in most, then self and total tables.
    /// </summary>
    private void DrawMethods()
    {
        if (_profileTask is { IsCompleted: true } task)
        {
            if (task.IsCompletedSuccessfully)
            {
                _profile = task.Result;
                (long from, long to) = _profile.Span;
                bool lines = _recent.Count > 0 && _recent[^1].Start >= from && _recent[0].Start <= to;
                Console.WriteLine($"CPU trace: {_profile.SampleCount:N0} samples on thread {_profile.Thread}; " +
                                  $"frame clock {(lines ? "matches" : $"doesn't match (samples {from}..{to}, frames {_recent[0].Start}..{_recent[^1].Start})")}");
            }
            else
                _profileError = task.Exception?.GetBaseException().Message ?? "The CPU trace couldn't be read.";
            _profileTask = null;
        }

        var line = new Rect2(Margin, MethodsTop + 60, Width - Margin * 2, 40);
        if (!_captures[_captureIndex].HasTrace)
        {
            Label(line, "This launch ran without the CPU sampler. Start it with XANADU_PROFILE=1, close it, and its methods show up here.", Palette.TextMuted, 20, center: true);
            return;
        }
        if (_profileError is not null)
        {
            Label(line, "The CPU trace couldn't be read: " + _profileError, Palette.Danger, 20, center: true);
            return;
        }
        if (_profile is null)
        {
            Label(line, "Reading the CPU trace…", Palette.TextMuted, 20, center: true);
            return;
        }

        MethodProfile profile = _profile;
        AutopsyFrame frame = _frame!;
        // Frames and samples share Linux's monotonic clock; if they don't overlap, a frame's samples can't be found.
        (long first, long last) = profile.Span;
        bool sameClock = _recent.Count > 0 && _recent[^1].Start >= first && _recent[0].Start <= last;
        bool canFrame = frame != _mean && frame.Start != 0 && sameClock;
        bool wholeLaunch = _wholeLaunch || !canFrame;
        Button(new Rect2(Margin, MethodsTop, 190, 38), "WHOLE LAUNCH", wholeLaunch, () => _wholeLaunch = true, "Every sample of the frame thread");
        Button(new Rect2(Margin + 196, MethodsTop, 190, 38), "THIS FRAME", !wholeLaunch, () => _wholeLaunch = false,
            canFrame ? "Only the samples taken during the picked frame"
            : sameClock ? "Pick a frame in the strip or the worst lists first" : "This trace's clock doesn't match the frames', so frames can't be picked out");

        (int[] self, int[] total, int count) = wholeLaunch
            ? profile.Count()
            : profile.Count(frame.Start, frame.Start + (long)(Ms(frame, FrameAutopsy.CpuRoot) / 1000.0 * _autopsy!.TicksPerSecond));
        double ms = count * profile.SampleMs;
        string scope = wholeLaunch ? "the whole launch" : $"frame {frame.Number:N0}";
        Label(new Rect2(Margin + 400, MethodsTop + 4, Right - Margin - 400, 30),
            $"{scope}  ·  thread {profile.Thread}  ·  {count:N0} samples ≈ {ms:0.0} ms  ·  one every {profile.SampleMs:0.00} ms  ·  waits count too",
            Palette.TextMuted, 17, right: true);
        if (count == 0)
        {
            Label(line, "No samples landed in this frame: it was shorter than the sampling interval, or the thread wasn't in managed code.", Palette.TextDim, 18, center: true);
            return;
        }

        var bySelf = Ranked(self);
        var bar = new Rect2(Left, MethodsTop + 56, Right - Left, BarHeight);
        Label(new Rect2(Margin, bar.Position.Y + 16, 100, 28), "SELF", Palette.TextMuted, 18);
        Canvas.DrawRect(Item, bar, Palette.SurfaceDeep);
        float x = bar.Position.X;
        int shown = 0, inBar = 0;
        foreach (int method in bySelf)
        {
            if (shown >= BarMethods)
                break;
            x = MethodSegment(method, self[method], total[method], count, profile.SampleMs, x, bar, Segments[shown % Segments.Length]);
            inBar += self[method];
            shown++;
        }
        if (count > inBar)
        {
            float width = (float)(count - inBar) / count * bar.Size.X;
            var rest = new Rect2(x, bar.Position.Y, width, bar.Size.Y);
            Canvas.DrawRect(Item, rest, Self);
            if (width > 120)
                Label(new Rect2(x + 6, bar.Position.Y, width - 12, bar.Size.Y), $"{bySelf.Count - shown} other methods", new Color(0.05f, 0.07f, 0.12f), 16);
            if (rest.HasPoint(_mouse))
                _hint = $"{bySelf.Count - shown} other methods  ·  {(count - inBar) * profile.SampleMs:0.0} ms  ·  {(count - inBar) * 100.0 / count:0.0}%";
        }

        float top = bar.End.Y + 30;
        float half = Width / 2f - Margin * 1.5f;
        MethodTable(Margin, top, half, "SELF: what the thread was running", bySelf, self, count, profile.SampleMs, colour: true);
        MethodTable(Width / 2f + Margin / 2, top, half, "TOTAL: including everything it called", Ranked(total), total, count, profile.SampleMs, colour: false);
    }

    private List<int> Ranked(int[] counts)
    {
        var order = new List<int>();
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
                order.Add(i);
        }
        order.Sort((a, b) => counts[b].CompareTo(counts[a]));
        return order;
    }

    private float MethodSegment(int method, int self, int total, int count, double sampleMs, float x, Rect2 bar, Color colour)
    {
        float width = (float)self / count * bar.Size.X;
        var box = new Rect2(x, bar.Position.Y, MathF.Max(width, 1), bar.Size.Y);
        bool hover = box.HasPoint(_mouse);
        Canvas.DrawRect(Item, box, hover ? colour.Lerp(Colors.White, 0.25f) : colour);
        Canvas.DrawRect(Item, new Rect2(box.End.X - 1, box.Position.Y, 1, box.Size.Y), Palette.Background);
        string name = _profile!.Methods[method];
        if (width > 80)
        {
            Color ink = new(0.05f, 0.07f, 0.12f);
            Label(new Rect2(x + 6, bar.Position.Y + 6, width - 12, 22), Fit(name, width - 12, 15), ink, 15);
            Label(new Rect2(x + 6, bar.Position.Y + 30, width - 12, 22), $"{self * sampleMs:0.0} ms", ink, 15);
        }
        if (hover)
            _hint = $"{name}  ·  self {self * sampleMs:0.0} ms ({self * 100.0 / count:0.0}%)  ·  total {total * sampleMs:0.0} ms ({total * 100.0 / count:0.0}%)";
        return x + width;
    }

    private void MethodTable(float left, float top, float width, string title, List<int> order, int[] counts, int count, double sampleMs, bool colour)
    {
        Label(new Rect2(left, top, width, 28), title, Palette.TextMuted, 18);
        float y = top + 34;
        for (int i = 0; i < order.Count && i < MethodRows; i++)
        {
            int method = order[i];
            string name = _profile!.Methods[method];
            var row = new Rect2(left, y, width, RowHeight);
            if (row.HasPoint(_mouse))
            {
                Canvas.DrawRect(Item, row, Palette.SurfaceRaised);
                _hint = name;
            }
            Canvas.DrawRect(Item, new Rect2(left, y + 7, 12, 12), colour && i < BarMethods ? Segments[i % Segments.Length] : Palette.BorderSubtle);
            Label(new Rect2(left + 20, y, width * 0.56f, RowHeight), Fit(name, width * 0.56f, 16), Palette.Text, 16);
            float barLeft = left + width * 0.58f, barWidth = width * 0.2f;
            Canvas.DrawRect(Item, new Rect2(barLeft, y + 8, barWidth, 10), Palette.SurfaceDeep);
            Canvas.DrawRect(Item, new Rect2(barLeft, y + 8, barWidth * counts[method] / count, 10), Palette.InfoSoft);
            Label(new Rect2(left + width * 0.78f, y, width * 0.22f, RowHeight),
                $"{counts[method] * sampleMs:0.0} ms  {counts[method] * 100.0 / count,5:0.0}%", Palette.TextMuted, 16, right: true);
            y += RowHeight;
        }
    }

    /// <summary>Cuts a name to what fits <paramref name="width"/> at <paramref name="size"/>, keeping its end (the method).</summary>
    private static string Fit(string text, float width, int size)
    {
        int max = Math.Max(4, (int)(width / (size * 0.52f)));
        return text.Length <= max ? text : "…" + text[^(max - 1)..];
    }

    // ---- bits -----------------------------------------------------------------------

    private void Button(Rect2 box, string label, bool on, Action click, string tooltip)
    {
        bool hover = box.HasPoint(_mouse);
        // Bright blue only for what's chosen; muted at rest.
        Color fill = on ? Palette.Primary : Palette.Muted;
        Canvas.DrawRect(Item, box, hover ? fill.Lerp(Colors.White, 0.16f) : fill);
        Label(box, label, Palette.TextBright, 17, center: true);
        if (hover)
        {
            _hint = tooltip;
            Clickable(box, click);
        }
    }

    private void Clickable(Rect2 box, Action click)
    {
        if (box.HasPoint(_mouse))
            _onClick = click;
    }

    private void DrawHint(string hint)
    {
        var box = new Rect2(Margin, Height - 44, Width - Margin * 2, 32);
        Canvas.DrawRect(Item, box, Palette.Tooltip);
        Label(new Rect2(box.Position.X + 12, box.Position.Y, box.Size.X - 24, box.Size.Y), hint, Palette.Text, 16);
    }

    private void Label(Rect2 box, string text, Color colour, int size, bool center = false, bool right = false)
    {
        if (center)
            Canvas.DrawTextStyled(Item, box, text, colour, size);
        else if (right)
            Canvas.DrawTextStyledRight(Item, box, text, colour, size);
        else
            Canvas.DrawTextStyledLeft(Item, box, text, colour, size);
    }

    private static float Measure(string text, int size) => text.Length * size * 0.55f;

}
