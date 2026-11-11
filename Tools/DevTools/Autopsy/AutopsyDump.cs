using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Host.Autopsy;

namespace Xanadu.Tools.Autopsy;

/// <summary>
/// <c>--dump[=app]</c>: the capture as text, no window: the mean frame and the
/// worst CPU frame as trees of milliseconds, slowest first, down to <see cref="Floor"/> ms.
/// </summary>
internal static class AutopsyDump
{
    private const float Floor = 0.005f;

    public static void Print(string? app, string? under = null)
    {
        List<AutopsyCapture> captures = AutopsyStore.List();
        AutopsyCapture? capture = app is null ? captures.FirstOrDefault() : captures.Find(c => c.App == app);
        if (capture is null)
        {
            Console.WriteLine(app is null ? "No captures." : $"No capture for '{app}'.");
            return;
        }

        FrameAutopsy autopsy = AutopsyStore.Load(capture);
        if (under is not null)
        {
            Methods(capture, autopsy, under);
            return;
        }
        var children = new List<int>[autopsy.Tracks.Count];
        for (int i = 0; i < children.Length; i++)
            children[i] = new List<int>();
        for (int i = 0; i < autopsy.Tracks.Count; i++)
            if (autopsy.Tracks[i].Parent >= 0)
                children[autopsy.Tracks[i].Parent].Add(i);

        Console.WriteLine($"{autopsy.App}: {autopsy.FrameCount:N0} frames over {autopsy.Seconds:0.0} s, budget {autopsy.BudgetMs:0.00} ms");
        AutopsyFrame? mean = autopsy.Frames.Find(f => f.Keep.HasFlag(AutopsyKeep.Mean));
        AutopsyFrame? worst = autopsy.Frames.Where(f => f.Keep.HasFlag(AutopsyKeep.WorstCpu))
            .OrderByDescending(f => f.Get(FrameAutopsy.CpuRoot)).FirstOrDefault();
        // Every system and pass by its worst frame among those kept (the recent ones and the launch's worst).
        Console.WriteLine();
        Console.WriteLine($"{"SLOWEST WORST CASES",-60}{"mean",10}{"max",10}  frame");
        var kept = autopsy.Frames.Where(f => !f.Keep.HasFlag(AutopsyKeep.Mean)).ToList();
        var peaks = Enumerable.Range(2, Math.Max(0, autopsy.Tracks.Count - 2))
            .Select(t => (Track: t, Peak: kept.OrderByDescending(f => Ms(f, t)).FirstOrDefault()))
            .Where(p => p.Peak is not null && children[p.Track].Count == 0)
            .OrderByDescending(p => Ms(p.Peak!, p.Track)).Take(25);
        foreach ((int track, AutopsyFrame? peak) in peaks)
            Console.WriteLine($"{Path(autopsy, track),-60}{(mean is null ? 0 : Ms(mean, track)),10:0.000}{Ms(peak!, track),10:0.000}  {peak!.Number}");

        foreach ((string title, AutopsyFrame? frame) in new[] { ("MEAN FRAME", mean), ("WORST CPU FRAME", worst) })
        {
            if (frame is null)
                continue;
            Console.WriteLine();
            Console.WriteLine(frame == mean ? title : $"{title} (frame {frame.Number})");
            Tree(autopsy, children, frame, FrameAutopsy.CpuRoot, 0, "CPU");
            Tree(autopsy, children, frame, FrameAutopsy.GpuRoot, 0, "GPU");
        }
    }

    private static void Tree(FrameAutopsy autopsy, List<int>[] children, AutopsyFrame frame, int track, int depth, string? name = null)
    {
        float ms = Ms(frame, track);
        Console.WriteLine($"{new string(' ', depth * 2)}{name ?? autopsy.Tracks[track].Name,-60}{ms,10:0.000}");
        foreach (int child in children[track].Where(c => Ms(frame, c) >= Floor).OrderByDescending(c => Ms(frame, c)))
            Tree(autopsy, children, frame, child, depth + 1);
    }

    /// <summary>The methods the frame thread ran beneath every method whose name contains <paramref name="under"/>.</summary>
    private static void Methods(AutopsyCapture capture, FrameAutopsy autopsy, string under)
    {
        if (!capture.HasTrace)
        {
            Console.WriteLine("This launch has no CPU trace (run it with XANADU_PROFILE=1).");
            return;
        }
        MethodProfile profile = MethodProfile.Read(capture, autopsy.MainThread);
        Console.WriteLine($"GC: {profile.Collections[0]} gen0, {profile.Collections[1]} gen1, {profile.Collections[2]} gen2 over {autopsy.Seconds:0.0} s; " +
                          $"threads held {profile.GcPauseMs:0.0} ms in all, longest {profile.LongestGcPauseMs:0.00} ms");
        for (int id = 0; id < profile.Methods.Count; id++)
        {
            if (!profile.Methods[id].Contains(under, StringComparison.OrdinalIgnoreCase))
                continue;
            (int[] self, int[] total, int count) = profile.Count(under: id);
            if (count < 5)
                continue;
            double all = profile.Count().Count;
            Console.WriteLine();
            Console.WriteLine($"UNDER {profile.Methods[id]}: {count:N0} samples, {count * profile.SampleMs:0.0} ms, {count / all * 100:0.0}% of the thread");
            Console.WriteLine($"  {"self",-80}{"ms",9}{"%",7}");
            foreach (int m in Enumerable.Range(0, self.Length).Where(m => self[m] > 0).OrderByDescending(m => self[m]).Take(25))
                Console.WriteLine($"  {Cut(profile.Methods[m], 80),-80}{self[m] * profile.SampleMs,9:0.0}{self[m] * 100.0 / count,7:0.0}");
            Console.WriteLine($"  {"total",-80}{"ms",9}{"%",7}");
            foreach (int m in Enumerable.Range(0, total.Length).Where(m => total[m] > 0).OrderByDescending(m => total[m]).Take(30))
                Console.WriteLine($"  {Cut(profile.Methods[m], 80),-80}{total[m] * profile.SampleMs,9:0.0}{total[m] * 100.0 / count,7:0.0}");
        }
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : "…" + text[^(max - 1)..];

    private static string Path(FrameAutopsy autopsy, int track)
    {
        var names = new List<string>();
        for (int t = track; t > 1; t = autopsy.Tracks[t].Parent)
            names.Insert(0, autopsy.Tracks[t].Name);
        string side = autopsy.Tracks[track].Side == AutopsySide.Cpu ? "CPU" : "GPU";
        string path = side + " / " + string.Join(" / ", names);
        return path.Length > 58 ? "…" + path[^57..] : path;
    }

    private static float Ms(AutopsyFrame frame, int track) => frame.Get(track) is var v && !float.IsNaN(v) ? v : 0;
}
