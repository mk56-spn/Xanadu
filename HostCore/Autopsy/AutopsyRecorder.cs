using System;
using System.Collections.Generic;
using System.Diagnostics;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Host.Autopsy;

/// <summary>
/// Records every frame of a launch for the Autopsy tool. CPU side: the wall
/// time from one update to the next, split into update (each top-level system,
/// down through its groups, from Friflo's perf counters), draw (waiting for the
/// swapchain, then recording and submitting) and idle (pacing and vsync). GPU
/// side: the <see cref="Rendering.GpuTimes"/> groups of the same frame, which
/// arrive a few frames later. Keeps the last <see cref="RecentCount"/> frames,
/// the launch's worst frames on each side and a running mean; HostApp saves it when the window closes.
/// </summary>
internal sealed class AutopsyRecorder
{
    public const int RecentCount = 1800;
    private const int WorstCount = 16;

    /// <summary>Frames waited for GPU results before a frame is kept without them.</summary>
    private const int GpuPatience = 16;

    private readonly FrameAutopsy _autopsy;
    private readonly Dictionary<(int Parent, string Name), int> _children = new();
    private readonly Dictionary<BaseSystem, (int Track, double Sum)> _systems = new();
    private readonly Queue<AutopsyFrame> _recent = new();
    private readonly List<AutopsyFrame> _worstCpu = new(), _worstGpu = new();
    private readonly Dictionary<long, AutopsyFrame> _pending = new();
    private readonly List<long> _settled = new();
    private readonly int _update, _draw, _acquire, _record, _idle;
    private double[] _sums = [];
    private long _finished;
    private long _frameStart;
    private double _updateMs, _drawMs, _acquireMs;
    private AutopsyFrame? _current;
    private SystemRoot? _monitored;
    private int _monitorAge;

    public AutopsyRecorder(string app, float budgetMs, long mainThread)
    {
        _autopsy = new FrameAutopsy
        {
            App = app, Started = DateTime.Now, BudgetMs = budgetMs, MainThread = mainThread, TicksPerSecond = Stopwatch.Frequency,
        };
        _autopsy.Tracks.Add(new AutopsyTrack("frame", -1, AutopsySide.Cpu));
        _autopsy.Tracks.Add(new AutopsyTrack("frame", -1, AutopsySide.Gpu));
        _update = Child(FrameAutopsy.CpuRoot, "update", AutopsySide.Cpu);
        _draw = Child(FrameAutopsy.CpuRoot, "draw", AutopsySide.Cpu);
        _idle = Child(FrameAutopsy.CpuRoot, "idle (pacing, vsync)", AutopsySide.Cpu);
        _acquire = Child(_draw, "wait for swapchain", AutopsySide.Cpu);
        _record = Child(_draw, "record + submit", AutopsySide.Cpu);
    }

    /// <summary>The frame being recorded, as the GPU "frame" group names it.</summary>
    public long Number { get; private set; } = -1;

    /// <summary>Closes the frame before and opens the next; call at the top of each update.</summary>
    public void BeginFrame()
    {
        long now = Stopwatch.GetTimestamp();
        if (_current is not null)
        {
            double interval = Stopwatch.GetElapsedTime(_frameStart, now).TotalMilliseconds;
            Set(_current, FrameAutopsy.CpuRoot, interval);
            Set(_current, _update, _updateMs);
            Set(_current, _draw, _drawMs);
            Set(_current, _acquire, _acquireMs);
            Set(_current, _record, _drawMs - _acquireMs);
            Set(_current, _idle, Math.Max(0, interval - _updateMs - _drawMs));
            _pending[_current.Number] = _current;
        }

        Number++;
        _frameStart = now;
        _updateMs = _drawMs = _acquireMs = 0;
        _current = new AutopsyFrame(Number, 0, NewValues(), now);
        Settle(GpuPatience);
    }

    public void AddUpdate(double ms) => _updateMs += ms;

    public void AddDraw(double ms, double acquireMs)
    {
        _drawMs += ms;
        _acquireMs += acquireMs;
    }

    /// <summary>Each system's time this frame: the change in its summed perf since the last sample.</summary>
    public void SampleSystems(SystemRoot root)
    {
        if (_current is null)
            return;
        // Groups can be added later, so turn timing on for nested ones now and then.
        if (root != _monitored || --_monitorAge <= 0)
        {
            Monitor(root);
            _monitored = root;
            _monitorAge = 120;
        }
        Sample(root, _update);
    }

    private static void Monitor(SystemGroup group) => Xanadu.Systems.Ui.AppStep.MonitorAll(group);

    /// <summary>Each system's time this frame, nested as the tree is; an app root the UI steps nests under its step.</summary>
    private void Sample(BaseSystem group, int parent)
    {
        foreach (BaseSystem system in Xanadu.Systems.Ui.AppStep.Children(group))
        {
            double sum = system.Perf.SumMs;
            if (!_systems.TryGetValue(system, out (int Track, double Sum) known))
                known = (Child(parent, string.IsNullOrEmpty(system.Name) ? system.GetType().Name : system.Name, AutopsySide.Cpu), sum);
            double ms = sum - known.Sum;
            _systems[system] = (known.Track, sum);
            if (ms > 0)
                Add(_current!, known.Track, ms);
            Sample(system, known.Track);
        }
    }

    /// <summary>One frame's GPU groups, depth 1 being the frame itself, in recording order.</summary>
    public void AddGpu(long number, ReadOnlySpan<(int Depth, string Name, double Ms)> ranges)
    {
        AutopsyFrame? frame = number == Number ? _current : _pending.GetValueOrDefault(number);
        if (frame is null)
            return;

        Span<int> parents = stackalloc int[33];
        foreach ((int depth, string name, double ms) in ranges)
        {
            if (depth < 1 || depth >= parents.Length)
                continue;
            int track = depth == 1 ? FrameAutopsy.GpuRoot : Child(parents[depth - 1], name, AutopsySide.Gpu);
            parents[depth] = track;
            Add(frame, track, ms);
        }

        if (frame != _current)
        {
            _pending.Remove(number);
            Finish(frame);
        }
    }

    /// <summary>Keeps frames whose GPU results never came (no swapchain, or results lost).</summary>
    private void Settle(int patience)
    {
        _settled.Clear();
        foreach (long number in _pending.Keys)
        {
            if (number <= Number - patience)
                _settled.Add(number);
        }
        _settled.Sort();
        foreach (long number in _settled)
        {
            Finish(_pending[number]);
            _pending.Remove(number);
        }
    }

    private void Finish(AutopsyFrame frame)
    {
        _finished++;
        if (_sums.Length < _autopsy.Tracks.Count)
            Array.Resize(ref _sums, _autopsy.Tracks.Count);
        for (int i = 0; i < frame.Values.Length; i++)
        {
            if (!float.IsNaN(frame.Values[i]))
                _sums[i] += frame.Values[i];
        }

        _recent.Enqueue(frame);
        while (_recent.Count > RecentCount)
            _recent.Dequeue();
        Rank(_worstCpu, frame, FrameAutopsy.CpuRoot);
        Rank(_worstGpu, frame, FrameAutopsy.GpuRoot);
    }

    private static void Rank(List<AutopsyFrame> worst, AutopsyFrame frame, int track)
    {
        float value = frame.Get(track);
        if (float.IsNaN(value) || (worst.Count == WorstCount && value <= worst[^1].Get(track)))
            return;
        int at = worst.FindIndex(other => value > other.Get(track));
        worst.Insert(at < 0 ? worst.Count : at, frame);
        if (worst.Count > WorstCount)
            worst.RemoveAt(worst.Count - 1);
    }

    /// <summary>Whether a CPU sample trace is being written alongside.</summary>
    public bool HasTrace { get; set; }

    /// <summary>Everything kept so far, ready to save.</summary>
    public FrameAutopsy Snapshot()
    {
        Settle(0);
        var kept = new Dictionary<long, AutopsyFrame>();
        void Keep(IEnumerable<AutopsyFrame> frames, AutopsyKeep why)
        {
            foreach (AutopsyFrame frame in frames)
            {
                if (!kept.TryGetValue(frame.Number, out AutopsyFrame? copy))
                {
                    copy = new AutopsyFrame(frame.Number, 0, frame.Values, frame.Start);
                    kept[frame.Number] = copy;
                }
                copy.Keep |= why;
            }
        }
        Keep(_recent, AutopsyKeep.Recent);
        Keep(_worstCpu, AutopsyKeep.WorstCpu);
        Keep(_worstGpu, AutopsyKeep.WorstGpu);

        var result = new FrameAutopsy
        {
            App = _autopsy.App,
            Started = _autopsy.Started,
            Seconds = (DateTime.Now - _autopsy.Started).TotalSeconds,
            FrameCount = _finished,
            BudgetMs = _autopsy.BudgetMs,
            MainThread = _autopsy.MainThread,
            TicksPerSecond = _autopsy.TicksPerSecond,
            HasTrace = HasTrace,
        };
        result.Tracks.AddRange(_autopsy.Tracks);

        var mean = new float[_autopsy.Tracks.Count];
        for (int i = 0; i < mean.Length; i++)
            mean[i] = _finished == 0 || i >= _sums.Length ? float.NaN : (float)(_sums[i] / _finished);
        result.Frames.Add(new AutopsyFrame(-1, AutopsyKeep.Mean, mean));

        var frames = new List<AutopsyFrame>(kept.Values);
        frames.Sort((a, b) => a.Number.CompareTo(b.Number));
        result.Frames.AddRange(frames);
        return result;
    }

    // ---- tracks and values --------------------------------------------------------

    private int Child(int parent, string name, AutopsySide side)
    {
        if (_children.TryGetValue((parent, name), out int track))
            return track;
        track = _autopsy.Tracks.Count;
        _autopsy.Tracks.Add(new AutopsyTrack(name, parent, side));
        _children[(parent, name)] = track;
        return track;
    }

    private float[] NewValues()
    {
        var values = new float[_autopsy.Tracks.Count];
        Array.Fill(values, float.NaN);
        return values;
    }

    private void Set(AutopsyFrame frame, int track, double ms)
    {
        Grow(frame, track);
        frame.Values[track] = (float)ms;
    }

    private void Add(AutopsyFrame frame, int track, double ms)
    {
        Grow(frame, track);
        float old = frame.Values[track];
        frame.Values[track] = float.IsNaN(old) ? (float)ms : old + (float)ms;
    }

    private void Grow(AutopsyFrame frame, int track)
    {
        if (track < frame.Values.Length)
            return;
        int old = frame.Values.Length;
        float[] values = frame.Values;
        Array.Resize(ref values, _autopsy.Tracks.Count);
        Array.Fill(values, float.NaN, old, values.Length - old);
        frame.Values = values;
    }
}
