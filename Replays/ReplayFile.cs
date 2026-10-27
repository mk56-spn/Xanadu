using System;
using System.IO;
using System.Text;
using Xanadu.Settings;

namespace Xanadu.Replays;

/// <summary>
/// The .xrs replay format. Little-endian binary:
/// <c>"XRS"</c>, a version byte, the header fields, the result, then the
/// events as a count followed by (time delta, action and down flag) pairs.
/// Time deltas are LEB128 varints, so a long replay stays small.
/// </summary>
public static class ReplayFile
{
    public const string Extension = ".xrs";
    public const byte Version = 1;

    private const byte DownFlag = 0x80;
    private static readonly byte[] Magic = "XRS"u8.ToArray();

    public static void Write(Stream stream, Replay replay)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(replay.LevelId);
        writer.Write(replay.LevelName);
        writer.Write(replay.SongId);
        writer.Write(replay.Player);
        writer.Write(replay.RecordedAt.ToUnixTimeMilliseconds());
        writer.Write(replay.NoteCount);

        ReplayResult r = replay.Result;
        writer.Write(r.Score);
        writer.Write(r.Accuracy);
        foreach (int count in new[]
                 {
                     r.MaxCombo, r.NotesHit, r.TotalNotes, r.FlawlessPlus, r.Flawless,
                     r.Clean, r.Fair, r.Deficient, r.Terrible, r.Miss,
                 })
        {
            writer.Write(count);
        }

        writer.Write(replay.Events.Count);
        int previous = 0;
        foreach (ReplayEvent replayEvent in replay.Events)
        {
            WriteVarint(writer, replayEvent.TimeMs - previous);
            writer.Write((byte)((int)replayEvent.Action | (replayEvent.Down ? DownFlag : 0)));
            previous = replayEvent.TimeMs;
        }
    }

    /// <exception cref="InvalidDataException">The data is not a readable replay.</exception>
    public static Replay Read(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
                throw new InvalidDataException("Not an .xrs replay.");

            byte version = reader.ReadByte();
            if (version != Version)
                throw new InvalidDataException($"Unsupported replay version {version}.");

            string levelId = reader.ReadString();
            string levelName = reader.ReadString();
            string songId = reader.ReadString();
            string player = reader.ReadString();
            var recordedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
            int noteCount = reader.ReadInt32();

            long score = reader.ReadInt64();
            double accuracy = reader.ReadDouble();
            var counts = new int[10];
            for (int i = 0; i < counts.Length; i++)
                counts[i] = reader.ReadInt32();

            int eventCount = reader.ReadInt32();
            if (eventCount < 0 || eventCount > 10_000_000)
                throw new InvalidDataException("Implausible event count.");

            var events = new ReplayEvent[eventCount];
            int time = 0;
            for (int i = 0; i < eventCount; i++)
            {
                time += ReadVarint(reader);
                byte packed = reader.ReadByte();
                int action = packed & ~DownFlag;
                if (action >= KeybindSettings.Actions.Length)
                    throw new InvalidDataException($"Unknown action {action}.");

                events[i] = new ReplayEvent(time, (KeybindAction)action, (packed & DownFlag) != 0);
            }

            return new Replay(
                levelId,
                levelName,
                songId,
                player,
                recordedAt,
                noteCount,
                new ReplayResult(
                    score, accuracy, counts[0], counts[1], counts[2], counts[3],
                    counts[4], counts[5], counts[6], counts[7], counts[8], counts[9]),
                events);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The replay file is truncated.", exception);
        }
    }

    private static void WriteVarint(BinaryWriter writer, int value)
    {
        // Events are time-ordered, but a negative delta would corrupt the stream.
        uint remaining = (uint)Math.Max(0, value);
        while (remaining >= 0x80)
        {
            writer.Write((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        writer.Write((byte)remaining);
    }

    private static int ReadVarint(BinaryReader reader)
    {
        uint result = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte next = reader.ReadByte();
            result |= (uint)(next & 0x7F) << shift;
            if ((next & 0x80) == 0)
                return (int)result;
        }

        throw new InvalidDataException("Malformed varint.");
    }
}
