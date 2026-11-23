using System.Threading;

namespace Xanadu.Tools.DevTools.Clock;

/// <summary><c>clock --dump[=seconds]</c>: measures the song clock for a while, muted, and prints the report; no window.</summary>
internal static class ClockDump
{
    public static void Print(double seconds)
    {
        using var probe = new ClockProbe();
        if (!probe.Start(0f))
        {
            Console.WriteLine(probe.Error);
            return;
        }

        Thread.Sleep(TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(ClockProbe.SettleMs));
        ClockReport r = probe.Report(probe.Recent(0));
        probe.Stop();

        Console.WriteLine($"{r.Seconds:0.0} s · device chunk {r.ChunkMs:0.00} ms · drift {r.DriftPpm:+0;-0} ppm · filled went backwards {r.Backwards}x");
        Console.WriteLine($"{"",-26}{"±p1-p99",9}{"stdev",8}{"min",8}{"median",8}{"max",8}{"n",8}");
        Line("raw off a steady line", r.RawJitter);
        Line("filled off a steady line", r.FilledJitter);
        Line("raw cursor step", r.RawSteps);
        Line("wall between chunks", r.ChunkGaps);
    }

    private static void Line(string name, Spread s) =>
        Console.WriteLine($"{name,-26}{s.Plus,9:0.00}{s.Stdev,8:0.00}{s.Min,8:0.00}{s.P50,8:0.00}{s.Max,8:0.00}{s.Count,8}");
}
