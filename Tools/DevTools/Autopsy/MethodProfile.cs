using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Xanadu.Host.Autopsy;

namespace Xanadu.Tools.Autopsy;

/// <summary>
/// The CPU samples of a launch (XANADU_PROFILE=1), read with TraceEvent: each
/// sample is one thread's managed call stack, taken about every millisecond.
/// Only the frame thread's samples are kept. A method's self time is the samples
/// it was on top of the stack in; its total time is the samples it was anywhere
/// in. Samples are wall time, so waiting (vsync, the swapchain) counts as well.
/// </summary>
internal sealed class MethodProfile
{
    /// <summary>One sample: when (EventPipe timestamp) and its stack as method ids, innermost first.</summary>
    private readonly record struct Sample(long Time, double Ms, int[] Stack);

    private readonly List<Sample> _samples = new();
    private readonly List<string> _methods = new();

    public IReadOnlyList<string> Methods => _methods;

    /// <summary>Milliseconds one sample stands for, measured from the samples' own spacing.</summary>
    public double SampleMs { get; private set; } = 1;

    public int SampleCount => _samples.Count;

    /// <summary>Garbage collections in the launch, by generation (0, 1, 2), and the time the runtime held every thread still for them.</summary>
    public int[] Collections { get; } = new int[3];
    public double GcPauseMs { get; private set; }
    public double LongestGcPauseMs { get; private set; }
    public long Thread { get; private set; }

    /// <summary>The first and last sample stamps, to check they share the frames' clock.</summary>
    public (long First, long Last) Span => _samples.Count == 0 ? (0, 0) : (_samples[0].Time, _samples[^1].Time);

    /// <summary>Each thread's sample count, busiest first: which threads ran managed code at all.</summary>
    public List<(long Thread, int Samples)> Threads { get; } = new();

    public static MethodProfile Read(AutopsyCapture capture, long mainThread)
    {
        string trace = AutopsyStore.TracePath(capture) ?? throw new FileNotFoundException("No CPU trace for this launch.");
        string etlx = Path.ChangeExtension(trace, ".etlx");
        if (!File.Exists(etlx) || File.GetLastWriteTimeUtc(etlx) < File.GetLastWriteTimeUtc(trace))
            TraceLog.CreateFromEventPipeDataFile(trace, etlx);

        var profile = new MethodProfile();
        var names = new Dictionary<string, int>();
        var perThread = new Dictionary<long, List<Sample>>();
        using var log = new TraceLog(etlx);
        var stack = new List<int>();
        double suspended = -1;
        foreach (TraceEvent sample in log.Events)
        {
            if (sample.ProviderName == "Microsoft-Windows-DotNETRuntime")
            {
                switch (sample.EventName)
                {
                    case "GC/Start" when sample.PayloadByName("Depth") is { } depth && Convert.ToInt32(depth) is >= 0 and < 3:
                        profile.Collections[Convert.ToInt32(depth)]++;
                        break;
                    // Only suspensions for a collection (reasons 1 and 6); the sampler suspends threads for every sample too.
                    case "GC/SuspendEEStart" when sample.PayloadByName("Reason") is { } reason && Convert.ToInt32(reason) is 1 or 6:
                        suspended = sample.TimeStampRelativeMSec;
                        break;
                    case "GC/RestartEEStop" when suspended >= 0:
                        double pause = sample.TimeStampRelativeMSec - suspended;
                        profile.GcPauseMs += pause;
                        profile.LongestGcPauseMs = Math.Max(profile.LongestGcPauseMs, pause);
                        suspended = -1;
                        break;
                }
                continue;
            }
            if (sample.ProviderName != "Microsoft-DotNETCore-SampleProfiler" || !sample.EventName.Contains("Sample"))
                continue;
            stack.Clear();
            for (TraceCallStack? frame = sample.CallStack(); frame is not null; frame = frame.Caller)
            {
                string name = Name(frame.CodeAddress);
                // The sampler stops threads at GC polls, so a sample often lands in the poll
                // itself; its time belongs to the method that polled.
                if (stack.Count == 0 && name.Contains("PollGC", StringComparison.Ordinal))
                    continue;
                if (!names.TryGetValue(name, out int id))
                {
                    id = profile._methods.Count;
                    profile._methods.Add(name);
                    names[name] = id;
                }
                stack.Add(id);
            }
            if (!perThread.TryGetValue(sample.ThreadID, out List<Sample>? list))
                perThread[sample.ThreadID] = list = new List<Sample>();
            // The raw EventPipe stamp: on Linux the monotonic clock in ns, as Stopwatch ticks are, so frames line up with samples.
#pragma warning disable CS0618
            list.Add(new Sample(sample.TimeStampQPC, sample.TimeStampRelativeMSec, stack.ToArray()));
#pragma warning restore CS0618
        }

        foreach ((long thread, List<Sample> list) in perThread)
            profile.Threads.Add((thread, list.Count));
        profile.Threads.Sort((a, b) => b.Samples.CompareTo(a.Samples));

        // The thread that ran the frames; the busiest one when the capture doesn't say.
        long chosen = perThread.ContainsKey(mainThread) ? mainThread : profile.Threads.Count > 0 ? profile.Threads[0].Thread : 0;
        profile.Thread = chosen;
        if (perThread.TryGetValue(chosen, out List<Sample>? samples))
        {
            samples.Sort((a, b) => a.Time.CompareTo(b.Time));
            profile._samples.AddRange(samples);
            if (samples.Count > 1)
            {
                profile.SampleMs = (samples[^1].Ms - samples[0].Ms) / (samples.Count - 1);
            }
        }
        return profile;
    }

    /// <summary>Class.Method, without the namespace and arguments; native or unnamed frames as their module.</summary>
    private static string Name(TraceCodeAddress address)
    {
        string full = address.FullMethodName;
        if (string.IsNullOrEmpty(full))
            return address.ModuleName is { Length: > 0 } module ? $"({module})" : "(native code)";
        int paren = full.IndexOf('(');
        string head = paren >= 0 ? full[..paren] : full;
        int method = head.LastIndexOf('.');
        int type = method > 0 ? head.LastIndexOf('.', method - 1) : -1;
        return type >= 0 ? head[(type + 1)..] : head;
    }

    /// <summary>
    /// Self and total sample counts per method for samples in [from, to) (all of them when from is 0),
    /// only those with <paramref name="under"/> somewhere on the stack when it is given.
    /// </summary>
    public (int[] Self, int[] Total, int Count) Count(long from = 0, long to = long.MaxValue, int under = -1)
    {
        var self = new int[_methods.Count];
        var total = new int[_methods.Count];
        var seen = new HashSet<int>();
        int count = 0;
        foreach (Sample sample in _samples)
        {
            if (sample.Time < from || sample.Time >= to || sample.Stack.Length == 0)
                continue;
            if (under >= 0 && Array.IndexOf(sample.Stack, under) < 0)
                continue;
            count++;
            self[sample.Stack[0]]++;
            seen.Clear();
            foreach (int method in sample.Stack)
            {
                if (seen.Add(method))
                    total[method]++;
            }
        }
        return (self, total, count);
    }
}
