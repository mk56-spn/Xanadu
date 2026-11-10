using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MoonWorks.Graphics;

namespace Xanadu.Host.Rendering;

/// <summary>
/// GPU time per debug group, measured on the GPU by our Vulkan layer
/// (HostCore/Native/gpu_times_layer.c): SDL_GPU has no timestamp queries, so
/// the layer puts them around every <see cref="GpuScopes.Scope"/> and every
/// command buffer. On whenever the layer is built (gcc and
/// External/fetch-vulkan-headers.sh); <c>XANADU_GPU_TIMES=0</c> turns it off,
/// <c>=1</c> also prints a tree of milliseconds per frame once a second. Each
/// frame's groups go to <see cref="FrameTimed"/> (the frame autopsy recorder).
/// </summary>
/// <remarks>
/// A group's time runs from when the GPU finished everything before it to when
/// it finished its own work, so children add up to their parent and nothing is
/// counted twice. Work SDL overlaps across groups lands in the later one.
/// </remarks>
internal static unsafe class GpuTimes
{
    private const string LayerName = "VK_LAYER_XANADU_gpu_times";
    private const string LayerFile = "libxanadu_gpu_times.so";
    private const string FramePrefix = "frame ";

    [StructLayout(LayoutKind.Sequential)]
    private struct Range
    {
        public fixed byte Name[56];
        public int Depth;
        public int Index;
        public ulong Serial;
        public double StartNs;
        public double DurationNs;
    }

    private sealed class Total
    {
        public double Ns;
        public int Count;
        public int Depth;
    }

    private static delegate* unmanaged<Range*, int, int> _read;
    private static delegate* unmanaged<int> _active;
    private static readonly Dictionary<string, Total> Totals = new();
    private static readonly List<string> Order = new();
    private static readonly string[] Names = new string[32];
    private static readonly Range[] Buffer = new Range[4096];
    private static int _frames;
    private static double _otherNs;
    private static int _otherCount;
    private static long _lastPrint = Environment.TickCount64;
    private static bool _warned, _print;
    private static readonly List<(int Depth, string Name, double Ms)> FrameRanges = new();

    /// <summary>One frame's groups once the GPU has run them: the frame number from its "frame N" group, then each group (depth 1 is the frame) in recording order.</summary>
    public delegate void FrameHandler(long number, ReadOnlySpan<(int Depth, string Name, double Ms)> ranges);

    public static FrameHandler? FrameTimed { get; set; }

    /// <summary>The name of a frame's outer group; <see cref="FrameTimed"/> gets the number back.</summary>
    public static string FrameName(long number) => FramePrefix + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True once the layer is loaded; debug groups are only recorded then.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>The last report printed, for an overlay to show.</summary>
    public static string Report { get; private set; } = "";

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetEnv(string name, string value, int overwrite);

    /// <summary>Puts the layer in Vulkan's chain. Must run before the graphics device is made.</summary>
    public static void Install()
    {
        string? setting = Environment.GetEnvironmentVariable("XANADU_GPU_TIMES");
        if (setting == "0" || !OperatingSystem.IsLinux())
            return;
        _print = setting == "1";

        string folder = AppContext.BaseDirectory.TrimEnd('/');
        string library = Path.Combine(folder, LayerFile);
        if (!File.Exists(library))
        {
            if (_print)
                Console.WriteLine($"GPU times: {LayerFile} isn't built; run External/fetch-vulkan-headers.sh and rebuild");
            return;
        }

        string? layers = Environment.GetEnvironmentVariable("VK_INSTANCE_LAYERS");
        SetEnv("VK_ADD_LAYER_PATH", folder, 1);
        SetEnv("VK_INSTANCE_LAYERS", string.IsNullOrEmpty(layers) ? LayerName : layers + ":" + LayerName, 1);

        // The loader opens the same file, so this handle reaches the layer's own ring.
        nint handle = NativeLibrary.Load(library);
        _read = (delegate* unmanaged<Range*, int, int>)NativeLibrary.GetExport(handle, "xgt_read");
        _active = (delegate* unmanaged<int>)NativeLibrary.GetExport(handle, "xgt_active");
        Enabled = true;
    }

    /// <summary>Drains finished ranges and prints the averages once a second (and when <paramref name="now"/> is set).</summary>
    public static void Poll(bool now = false)
    {
        if (!Enabled)
            return;

        if (_active() == 0)
        {
            if (!_warned)
                Console.WriteLine("GPU times: the layer isn't in the device chain (is the Vulkan loader older than 1.3.234?)");
            _warned = true;
            return;
        }

        int count;
        fixed (Range* buffer = Buffer)
        {
            while ((count = _read(buffer, Buffer.Length)) > 0)
                Add(buffer, count);
        }

        if (!_print || (!now && Environment.TickCount64 - _lastPrint < 1000))
            return;
        _lastPrint = Environment.TickCount64;
        if (_frames == 0)
            return;

        Print();
        Totals.Clear();
        Order.Clear();
        _frames = 0;
        _otherNs = 0;
        _otherCount = 0;
    }

    private static void Add(Range* ranges, int count)
    {
        // Ranges arrive grouped by command buffer, in recording order with depths.
        int start = 0;
        while (start < count)
        {
            int end = start;
            while (end < count && ranges[end].Serial == ranges[start].Serial)
                end++;
            AddCommandBuffer(ranges, start, end);
            start = end;
        }
    }

    private static void AddCommandBuffer(Range* ranges, int start, int end)
    {
        long number = -1;
        for (int i = start; i < end && number < 0; i++)
        {
            if (ranges[i].Depth == 1 && Name(ref ranges[i]) is var name && name.StartsWith(FramePrefix, StringComparison.Ordinal))
                number = long.TryParse(name.AsSpan(FramePrefix.Length), out long parsed) ? parsed : -1;
        }

        if (number < 0)
        {
            // Uploads, screenshots and the like: one line for all of them.
            for (int i = start; i < end; i++)
            {
                if (ranges[i].Depth == 0)
                {
                    _otherNs += ranges[i].DurationNs;
                    _otherCount++;
                }
            }
            return;
        }

        _frames++;
        FrameRanges.Clear();
        for (int i = start; i < end; i++)
        {
            ref Range range = ref ranges[i];
            if (range.Depth == 0)
                continue;
            int depth = Math.Min(range.Depth, Names.Length - 1);
            Names[depth] = depth == 1 ? "frame" : Name(ref range);
            FrameRanges.Add((depth, Names[depth], range.DurationNs / 1e6));
            string key = string.Join(" / ", Names, 1, depth);
            if (!Totals.TryGetValue(key, out Total? total))
            {
                total = new Total { Depth = depth - 1 };
                Totals[key] = total;
                Order.Add(key);
            }
            total.Ns += range.DurationNs;
            total.Count++;
        }
        FrameTimed?.Invoke(number, CollectionsMarshal.AsSpan(FrameRanges));
    }

    private static string Name(ref Range range)
    {
        fixed (byte* name = range.Name)
            return Marshal.PtrToStringUTF8((nint)name) ?? "?";
    }

    private static void Print()
    {
        var text = new StringBuilder();
        text.AppendLine($"GPU times: ms per frame, mean of {_frames} frames");
        foreach (string key in Order)
        {
            Total total = Totals[key];
            string name = new string(' ', total.Depth * 2) + key[(key.LastIndexOf(" / ", StringComparison.Ordinal) is var cut and >= 0 ? cut + 3 : 0)..];
            double ms = total.Ns / 1e6 / _frames;
            double calls = (double)total.Count / _frames;
            string each = calls > 1.01 ? $"  ×{calls:0.#}" : "";
            text.AppendLine($"  {name,-44} {ms,8:0.000}{each}");
        }
        if (_otherCount > 0)
            text.AppendLine($"  {"other command buffers",-44} {_otherNs / 1e6 / _frames,8:0.000}  ×{(double)_otherCount / _frames:0.#}");
        Report = text.ToString();
        Console.Write(Report);
    }
}

/// <summary>A named stretch of GPU work, timed by <see cref="GpuTimes"/>. Free when timing is off.</summary>
internal readonly ref struct GpuScope
{
    private readonly CommandBuffer? _commands;

    public GpuScope(CommandBuffer commands, string name)
    {
        if (!GpuTimes.Enabled)
            return;
        _commands = commands;
        commands.PushDebugGroup(name);
    }

    public void Dispose() => _commands?.PopDebugGroup();
}

internal static class GpuScopes
{
    /// <summary><c>using var _ = commands.Scope("bloom");</c> times everything recorded until the scope ends. Open and close it on the same side of a render pass's begin and end.</summary>
    public static GpuScope Scope(this CommandBuffer commands, string name) => new(commands, name);
}
