using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Xanadu.Lab;

/// <summary>
/// GPU time per named pass, from RenderingDevice timestamps. Call
/// <see cref="Begin"/> before a run of passes and <see cref="Mark"/> after each
/// one: a pass's time is the GPU time between its mark and the mark before it.
/// Marks go through the render thread's queue, so they land between the blits
/// queued around them. Timestamps come back a few frames late; <see cref="Collect"/>
/// once a frame picks up whichever frame is newest.
/// </summary>
public sealed class GpuPassTimer(string prefix)
{
    private const string BeginName = "begin";

    private static readonly bool DumpNames = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--dump-timestamps");

    private readonly object _lock = new();
    private readonly Dictionary<string, double> _totals = new();
    private readonly List<string> _order = new();
    private ulong _lastFrame = ulong.MaxValue;
    private int _frames;
    private bool _dumped;

    /// <summary>Starts a run of passes.</summary>
    public void Begin() => Capture(prefix + BeginName);

    /// <summary>Ends the pass <paramref name="pass"/>, which began at the previous mark.</summary>
    public void Mark(string pass) => Capture(prefix + pass);

    /// <summary>Reads the newest frame's timestamps, once per frame.</summary>
    public void Collect() => RenderingServer.CallOnRenderThread(Callable.From(Read));

    /// <summary>
    /// Average milliseconds per frame for each pass since the last report, in
    /// first-seen order, then clears the totals. Empty until a frame has come back.
    /// </summary>
    public (string Pass, double Ms)[] Report()
    {
        lock (_lock)
        {
            if (_frames == 0)
                return [];
            var report = _order.Select(p => (p, _totals[p] / _frames)).ToArray();
            foreach (string pass in _order)
                _totals[pass] = 0;
            _frames = 0;
            return report;
        }
    }

    private static void Capture(string name) =>
        RenderingServer.CallOnRenderThread(Callable.From(() => RenderingServer.GetRenderingDevice()?.CaptureTimestamp(name)));

    private void Read()
    {
        RenderingDevice? rd = RenderingServer.GetRenderingDevice();
        if (rd is null)
            return;
        ulong frame = rd.GetCapturedTimestampsFrame();
        if (frame == _lastFrame)
            return;
        _lastFrame = frame;

        uint count = rd.GetCapturedTimestampsCount();
        if (DumpNames && !_dumped && count > 0)
        {
            _dumped = true;
            for (uint i = 0; i < count; i++)
                GD.Print($"[timestamps] {i,3} {rd.GetCapturedTimestampName(i)}  {rd.GetCapturedTimestampGpuTime(i)}");
        }

        lock (_lock)
        {
            ulong previous = 0;
            bool open = false;
            for (uint i = 0; i < count; i++)
            {
                string name = rd.GetCapturedTimestampName(i);
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                ulong time = rd.GetCapturedTimestampGpuTime(i);
                string pass = name[prefix.Length..];
                if (pass != BeginName && open)
                {
                    if (!_totals.ContainsKey(pass))
                    {
                        _totals[pass] = 0;
                        _order.Add(pass);
                    }
                    _totals[pass] += (time - previous) / 1e6;
                }
                previous = time;
                open = true;
            }
            _frames++;
        }
    }
}
