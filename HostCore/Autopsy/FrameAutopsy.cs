using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Storage;

namespace Xanadu.Host.Autopsy;

/// <summary>Which side of the frame a track measures.</summary>
public enum AutopsySide : byte
{
    Cpu = 0,
    Gpu = 1,
}

/// <summary>Why a frame was kept; a frame can be kept for more than one reason.</summary>
[Flags]
public enum AutopsyKeep : byte
{
    Recent = 1,
    WorstCpu = 2,
    WorstGpu = 4,
    Mean = 8,
}

/// <summary>
/// One timed thing: a system, a host phase or a GPU group. Tracks form two
/// trees (CPU and GPU) through <see cref="Parent"/>; a parent's time includes its children's.
/// </summary>
public sealed record AutopsyTrack(string Name, int Parent, AutopsySide Side);

/// <summary>One frame's milliseconds per track, NaN where a track didn't run or wasn't known yet.</summary>
public sealed class AutopsyFrame(long number, AutopsyKeep keep, float[] values, long start = 0)
{
    public long Number { get; } = number;
    /// <summary>When the frame began, in <see cref="System.Diagnostics.Stopwatch"/> ticks (on Linux the same clock EventPipe stamps samples with); 0 for the mean frame.</summary>
    public long Start { get; } = start;
    public AutopsyKeep Keep { get; set; } = keep;
    public float[] Values { get; set; } = values;

    public float Get(int track) => track < Values.Length ? Values[track] : float.NaN;
}

/// <summary>
/// The capture of one launch, written by <see cref="AutopsyRecorder"/> and read
/// by the Autopsy tool: the last seconds of frames, the launch's worst frames and its mean frame.
/// </summary>
public sealed class FrameAutopsy
{
    /// <summary>Which exe ran: "game", "labs", "animator"…</summary>
    public string App { get; set; } = "";
    public DateTime Started { get; set; }
    public double Seconds { get; set; }
    public long FrameCount { get; set; }
    /// <summary>The frame time the display allows, in milliseconds.</summary>
    public float BudgetMs { get; set; } = 1000f / 60f;
    /// <summary>The OS thread id of the thread that ran the frames, to pick its samples out of the CPU trace.</summary>
    public long MainThread { get; set; }
    /// <summary><see cref="System.Diagnostics.Stopwatch.Frequency"/> when recorded: frame starts are in these ticks.</summary>
    public long TicksPerSecond { get; set; }
    /// <summary>Whether this launch also wrote a CPU sample trace (XANADU_PROFILE=1).</summary>
    public bool HasTrace { get; set; }
    public List<AutopsyTrack> Tracks { get; } = new();
    public List<AutopsyFrame> Frames { get; } = new();

    public const int CpuRoot = 0;
    public const int GpuRoot = 1;
}

/// <summary>
/// The binary .xfa file (Xanadu frame autopsy): the shared chunked frame with a
/// string table, the launch's meta, its tracks and its kept frames.
/// </summary>
public static class XfaBinary
{
    public const ushort MajorVersion = 0;
    public const ushort MinorVersion = 2;
    public const string Extension = ".xfa";

    private const string Format = "XFA";
    private const uint MaxTracks = 100_000;
    private const uint MaxFrames = 1_000_000;
    private static readonly byte[] Magic = "XFA1"u8.ToArray();

    private enum Chunk : uint
    {
        Strings = 1,
        Meta = 2,
        Tracks = 3,
        Frames = 4,
    }

    public static byte[] Write(FrameAutopsy autopsy)
    {
        var strings = new StringTable(Format);
        strings.Add(autopsy.App);
        foreach (AutopsyTrack track in autopsy.Tracks)
            strings.Add(track.Name);

        byte[] meta = Payload(writer =>
        {
            strings.WriteIndex(writer, autopsy.App);
            writer.Write(autopsy.Started.ToUniversalTime().Ticks);
            writer.Write(autopsy.Seconds);
            writer.Write(autopsy.FrameCount);
            writer.Write(autopsy.BudgetMs);
            writer.Write(autopsy.MainThread);
            writer.Write(autopsy.TicksPerSecond);
            writer.Write(autopsy.HasTrace);
        });
        byte[] tracks = Payload(writer =>
        {
            writer.Write((uint)autopsy.Tracks.Count);
            foreach (AutopsyTrack track in autopsy.Tracks)
            {
                strings.WriteIndex(writer, track.Name);
                writer.Write(track.Parent);
                writer.Write((byte)track.Side);
            }
        });
        byte[] frames = Payload(writer =>
        {
            writer.Write((uint)autopsy.Frames.Count);
            foreach (AutopsyFrame frame in autopsy.Frames)
            {
                writer.Write(frame.Number);
                writer.Write(frame.Start);
                writer.Write((byte)frame.Keep);
                writer.Write((uint)frame.Values.Length);
                foreach (float value in frame.Values)
                    writer.Write(value);
            }
        });

        var file = new ChunkWriter(Magic, MajorVersion, MinorVersion);
        file.Add((uint)Chunk.Strings, strings.Write());
        file.Add((uint)Chunk.Meta, meta);
        file.Add((uint)Chunk.Tracks, tracks);
        file.Add((uint)Chunk.Frames, frames);
        return file.ToArray();
    }

    public static FrameAutopsy Read(byte[] data)
    {
        try
        {
            ChunkReader file = ChunkReader.Open(data, Magic, MajorVersion, Format);
            StringTable strings = StringTable.Read(file.Get((uint)Chunk.Strings, Format), Format);

            var autopsy = new FrameAutopsy();
            using (BinaryReader reader = file.Get((uint)Chunk.Meta, Format).OpenReader())
            {
                autopsy.App = strings.ReadIndex(reader);
                autopsy.Started = new DateTime(reader.ReadInt64(), DateTimeKind.Utc).ToLocalTime();
                autopsy.Seconds = reader.ReadDouble();
                autopsy.FrameCount = reader.ReadInt64();
                autopsy.BudgetMs = reader.ReadSingle();
                if (file.Minor >= 2)
                {
                    autopsy.MainThread = reader.ReadInt64();
                    autopsy.TicksPerSecond = reader.ReadInt64();
                    autopsy.HasTrace = reader.ReadBoolean();
                }
            }

            using (BinaryReader reader = file.Get((uint)Chunk.Tracks, Format).OpenReader())
            {
                uint count = Count(reader, MaxTracks, "track");
                for (uint i = 0; i < count; i++)
                {
                    string name = strings.ReadIndex(reader);
                    int parent = reader.ReadInt32();
                    var side = (AutopsySide)reader.ReadByte();
                    if (parent >= (int)i || parent < -1)
                        throw new FormatException("XFA track parent out of order.");
                    autopsy.Tracks.Add(new AutopsyTrack(name, parent, side));
                }
            }

            using (BinaryReader reader = file.Get((uint)Chunk.Frames, Format).OpenReader())
            {
                uint count = Count(reader, MaxFrames, "frame");
                for (uint i = 0; i < count; i++)
                {
                    long number = reader.ReadInt64();
                    long start = file.Minor >= 2 ? reader.ReadInt64() : 0;
                    var keep = (AutopsyKeep)reader.ReadByte();
                    uint values = Count(reader, (uint)autopsy.Tracks.Count, "value");
                    var array = new float[values];
                    for (int v = 0; v < array.Length; v++)
                        array[v] = reader.ReadSingle();
                    autopsy.Frames.Add(new AutopsyFrame(number, keep, array, start));
                }
            }

            return autopsy;
        }
        catch (EndOfStreamException exception)
        {
            throw new FormatException("XFA file ends early.", exception);
        }
    }

    private static uint Count(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        return count <= max ? count : throw new FormatException($"Too many XFA {what}s.");
    }

    private static byte[] Payload(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            write(writer);
        return stream.ToArray();
    }
}
