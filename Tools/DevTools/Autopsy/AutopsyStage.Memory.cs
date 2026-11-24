using System.Collections.Generic;
using Xanadu.Core.Theme;
using Xanadu.Host.Autopsy;

namespace Xanadu.Tools.DevTools.Autopsy;

/// <summary>
/// The MEMORY view: over the strip's frames, the managed heap, the process and
/// the GPU memory each in a lane scaled to its own range (so a slow climb shows),
/// then what every frame allocated with its garbage collections marked on top.
/// Under them, the picked frame's figures and the launch's totals; GC FRAMES
/// steps through the last frames that collected.
/// </summary>
internal sealed partial class AutopsyStage
{
    private const float MemoryTop = 410;
    private const float MemoryLane = 66, MemoryGap = 14;
    private const float AllocLane = 110;
    private const float MemoryTableTop = 820;

    private int _allocatedTrack = -1, _heapTrack = -1, _processTrack = -1, _gpuTrack = -1;
    private int _gen0Track = -1, _gen1Track = -1, _gen2Track = -1, _pauseTrack = -1;
    private readonly List<AutopsyFrame> _gcFrames = [];
    private int _gcIndex = -1;

    private static readonly Color Gen0Colour = Palette.Cyan, Gen1Colour = Palette.Warning, Gen2Colour = Palette.Danger;

    private void OpenMemory()
    {
        _allocatedTrack = MemoryTrack(AutopsyMemory.AllocatedKb);
        _heapTrack = MemoryTrack(AutopsyMemory.HeapMb);
        _processTrack = MemoryTrack(AutopsyMemory.ProcessMb);
        _gpuTrack = MemoryTrack(AutopsyMemory.GpuMb);
        _gen0Track = MemoryTrack(AutopsyMemory.Gen0);
        _gen1Track = MemoryTrack(AutopsyMemory.Gen1);
        _gen2Track = MemoryTrack(AutopsyMemory.Gen2);
        _pauseTrack = MemoryTrack(AutopsyMemory.PauseMs);
        _gcFrames.Clear();
        foreach (AutopsyFrame frame in _autopsy!.Frames)
        {
            if (frame.Keep.HasFlag(AutopsyKeep.Gc))
                _gcFrames.Add(frame);
        }
        _gcIndex = -1;
    }

    private int MemoryTrack(string name) =>
        _autopsy!.Tracks.FindIndex(track => track.Side == AutopsySide.Memory && track.Parent < 0 && track.Name == name);

    private void DrawMemory()
    {
        if (_allocatedTrack < 0)
        {
            Label(new Rect2(Margin, MemoryTop + 60, Width - Margin * 2, 40),
                "This capture was saved before memory was recorded: run the exe again and close it.", Palette.TextMuted, 20, center: true);
            return;
        }

        float y = MemoryTop;
        y = Level(y, "HEAP", _heapTrack, Palette.Violet, "managed heap: what the GC holds, live or not yet collected");
        y = Level(y, "PROCESS", _processTrack, Palette.InfoSoft, "the whole process in RAM: managed, native, driver; read twice a second");
        y = Level(y, "GPU", _gpuTrack, Palette.Lime, "video memory this process holds, as the driver reports it; read twice a second");
        DrawAllocations(y);
        DrawMemoryTables();
    }

    /// <summary>One memory level over the recent frames, scaled from its lowest to its highest, with the range at the side.</summary>
    private float Level(float top, string title, int track, Color colour, string about)
    {
        var lane = new Rect2(Left, top, Right - Left, MemoryLane);
        Label(new Rect2(Margin, top + 6, 110, 24), title, Palette.TextMuted, 18);
        Canvas.DrawRect(Item, lane, Palette.SurfaceDeep);

        float low = float.MaxValue, high = float.MinValue;
        foreach (AutopsyFrame frame in _recent)
        {
            float value = frame.Get(track);
            if (float.IsNaN(value))
                continue;
            low = MathF.Min(low, value);
            high = MathF.Max(high, value);
        }
        if (low > high)
        {
            Label(lane, "not recorded", Palette.TextDim, 16, center: true);
            return top + MemoryLane + MemoryGap;
        }
        Label(new Rect2(Margin, top + 32, 110, 22), $"{high - low:0.0} MB", Palette.TextDim, 14);
        Label(new Rect2(Right + 4, top - 2, Width - Right - 4, 18), $"{high:0}", Palette.TextDim, 13);
        Label(new Rect2(Right + 4, lane.End.Y - 16, Width - Right - 4, 18), $"{low:0}", Palette.TextDim, 13);

        // A flat line sits mid-lane; otherwise the range fills it.
        float span = MathF.Max(high - low, 0.5f);
        float floor = high - low < 0.5f ? low - 0.25f : low;
        float step = lane.Size.X / Math.Max(1, _recent.Count);
        for (int i = 0; i < _recent.Count; i++)
        {
            AutopsyFrame frame = _recent[i];
            float value = frame.Get(track);
            if (float.IsNaN(value))
                continue;
            float x = lane.Position.X + i * step;
            float h = 4 + (value - floor) / span * (MemoryLane - 8);
            Canvas.DrawRect(Item, new Rect2(x, lane.End.Y - h, MathF.Max(step, 1), 2), colour);
            Canvas.DrawRect(Item, new Rect2(x, lane.End.Y - h + 2, MathF.Max(step, 1), h - 2), colour with { A = 0.16f });
            HoverFrame(new Rect2(x, lane.Position.Y, MathF.Max(step, 1), MemoryLane), frame, $"{title.ToLowerInvariant()} {value:0.0} MB  ·  {about}");
        }
        return top + MemoryLane + MemoryGap;
    }

    /// <summary>Every frame's allocations as bars (scaled so one burst doesn't flatten the rest), its collections as ticks above.</summary>
    private void DrawAllocations(float top)
    {
        var lane = new Rect2(Left, top + 14, Right - Left, AllocLane);
        Label(new Rect2(Margin, top + 20, 110, 24), "ALLOC", Palette.TextMuted, 18);
        Label(new Rect2(Margin, top + 46, 110, 22), "KB / frame", Palette.TextDim, 14);
        Canvas.DrawRect(Item, lane, Palette.SurfaceDeep);

        var values = new List<float>(_recent.Count);
        foreach (AutopsyFrame frame in _recent)
            values.Add(Ms(frame, _allocatedTrack));
        values.Sort();
        float scale = values.Count == 0 ? 1 : MathF.Max(1, values[(int)(values.Count * 0.95f)] * 1.25f);
        Label(new Rect2(Right + 4, lane.Position.Y - 2, Width - Right - 4, 18), $"{scale:0}", Palette.TextDim, 13);

        float step = lane.Size.X / Math.Max(1, _recent.Count);
        for (int i = 0; i < _recent.Count; i++)
        {
            AutopsyFrame frame = _recent[i];
            float x = lane.Position.X + i * step;
            float width = MathF.Max(step - (step > 3 ? 1 : 0), 1);
            float kb = Ms(frame, _allocatedTrack);
            float h = MathF.Min(kb / scale * AllocLane, AllocLane);
            Canvas.DrawRect(Item, new Rect2(x, lane.End.Y - h, width, h), Palette.Pink);
            if (kb > scale)
                Canvas.DrawRect(Item, new Rect2(x, lane.Position.Y, width, 3), Palette.Danger);

            if (GcColour(frame) is { } gc)
                Canvas.DrawRect(Item, new Rect2(x, top, MathF.Max(width, 3), 10), gc);
            if (frame == _frame)
                Canvas.DrawRect(Item, new Rect2(x - 1, lane.End.Y + 2, MathF.Max(width, 2) + 2, 4), Palette.Accent);
            HoverFrame(new Rect2(x, top, MathF.Max(step, 1), lane.End.Y - top), frame, null);
        }
    }

    /// <summary>The tick colour of the oldest generation a frame collected; null when it collected nothing.</summary>
    private Color? GcColour(AutopsyFrame frame) =>
        Ms(frame, _gen2Track) > 0 ? Gen2Colour : Ms(frame, _gen1Track) > 0 ? Gen1Colour : Ms(frame, _gen0Track) > 0 ? Gen0Colour : null;

    private void HoverFrame(Rect2 column, AutopsyFrame frame, string? what)
    {
        if (!column.HasPoint(_mouse))
            return;
        Canvas.DrawRect(Item, column, new Color(1, 1, 1, 0.12f));
        _hint = $"frame {frame.Number}  ·  " + (what ?? MemoryLine(frame));
        Clickable(column, () => Pick(frame));
    }

    private string MemoryLine(AutopsyFrame frame)
    {
        string line = $"allocated {Ms(frame, _allocatedTrack):0.0} KB  ·  heap {Ms(frame, _heapTrack):0.0} MB";
        if (GcColour(frame) is not null)
            line += $"  ·  GC gen0 {Ms(frame, _gen0Track):0} gen1 {Ms(frame, _gen1Track):0} gen2 {Ms(frame, _gen2Track):0}, paused {Ms(frame, _pauseTrack):0.00} ms";
        return line + $"  ·  CPU {Ms(frame, FrameAutopsy.CpuRoot):0.00} ms";
    }

    /// <summary>The picked frame's figures on the left, the launch's on the right, GC FRAMES above them.</summary>
    private void DrawMemoryTables()
    {
        AutopsyFrame frame = _frame!;
        float half = Width / 2f - Margin * 1.5f;
        float left = Margin, right = Width / 2f + Margin / 2;

        bool showing = _gcIndex >= 0 && _gcIndex < _gcFrames.Count && _frame == _gcFrames[_gcIndex];
        string text = showing ? $"GC FRAMES  {_gcIndex + 1}/{_gcFrames.Count}" : $"GC FRAMES  {_gcFrames.Count}";
        float bx = right + half - 284;
        Button(new Rect2(bx, MemoryTableTop - 8, 200, 34), text, showing, () => ShowGc(_gcFrames.Count - 1),
            "The launch's last frames that collected garbage, oldest to newest; opens on the newest");
        Button(new Rect2(bx + 204, MemoryTableTop - 8, 38, 34), "<", false, () => ShowGc(_gcIndex - 1), "Older");
        Button(new Rect2(bx + 246, MemoryTableTop - 8, 38, 34), ">", false, () => ShowGc(_gcIndex + 1), "Newer");

        bool mean = frame == _mean;
        Label(new Rect2(left, MemoryTableTop, half, 28), mean ? "MEAN FRAME" : $"FRAME {frame.Number:N0}", Palette.TextMuted, 18);
        float y = MemoryTableTop + 34;
        y = Row(left, y, half, "allocated", $"{Ms(frame, _allocatedTrack):0.0} KB");
        y = Row(left, y, half, "heap", $"{Ms(frame, _heapTrack):0.0} MB");
        y = Row(left, y, half, "process", Mb(frame, _processTrack));
        y = Row(left, y, half, "GPU", Mb(frame, _gpuTrack));
        string format = mean ? "0.000" : "0";
        y = Row(left, y, half, "collections gen0 · gen1 · gen2",
            $"{Ms(frame, _gen0Track).ToString(format)} · {Ms(frame, _gen1Track).ToString(format)} · {Ms(frame, _gen2Track).ToString(format)}");
        Row(left, y, half, "GC pause", $"{Ms(frame, _pauseTrack):0.000} ms");

        // The mean frame times the frame count gives the launch's totals.
        Label(new Rect2(right, MemoryTableTop, half - 300, 28), "WHOLE LAUNCH", Palette.TextMuted, 18);
        y = MemoryTableTop + 34;
        if (_mean is null)
            return;
        long frames = _autopsy!.FrameCount;
        double seconds = Math.Max(_autopsy.Seconds, 1e-3);
        double allocatedMb = Ms(_mean, _allocatedTrack) * frames / 1024.0;
        y = Row(right, y, half, "allocated", $"{allocatedMb:N0} MB  ·  {allocatedMb / seconds:0.0} MB/s  ·  {Ms(_mean, _allocatedTrack):0.0} KB/frame");
        y = Row(right, y, half, "heap peak (strip)", $"{Peak(_heapTrack):0.0} MB");
        y = Row(right, y, half, "process peak (strip)", $"{Peak(_processTrack):0} MB");
        y = Row(right, y, half, "collections gen0 · gen1 · gen2",
            $"{Ms(_mean, _gen0Track) * frames:0} · {Ms(_mean, _gen1Track) * frames:0} · {Ms(_mean, _gen2Track) * frames:0}");
        double pausedMs = Ms(_mean, _pauseTrack) * frames;
        Row(right, y, half, "GC pause", $"{pausedMs:0.0} ms in all  ·  {pausedMs / (seconds * 10):0.00}% of the launch");
    }

    private float Row(float left, float y, float width, string name, string value)
    {
        Label(new Rect2(left, y, width * 0.5f, RowHeight), name, Palette.Text, 16);
        Label(new Rect2(left + width * 0.5f, y, width * 0.5f, RowHeight), value, Palette.TextMuted, 16, right: true);
        return y + RowHeight;
    }

    private static string Mb(AutopsyFrame frame, int track) =>
        frame.Get(track) is var value && !float.IsNaN(value) ? $"{value:0.0} MB" : "n/a";

    private float Peak(int track)
    {
        float peak = 0;
        foreach (AutopsyFrame frame in _recent)
            peak = MathF.Max(peak, Ms(frame, track));
        return peak;
    }

    /// <summary>Steps through the GC frames, kept oldest first.</summary>
    private void ShowGc(int index)
    {
        if (_gcFrames.Count == 0)
            return;
        _gcIndex = Math.Clamp(index, 0, _gcFrames.Count - 1);
        Pick(_gcFrames[_gcIndex]);
    }
}
