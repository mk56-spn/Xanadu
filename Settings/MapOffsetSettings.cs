using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xanadu.Settings;

/// <summary>
/// The player's own offset for each map, added to the global one. It belongs to this
/// player's machine, not the map, so it lives apart from level files. The .xmo format
/// is little-endian binary: <c>"XMO"</c>, a major and a minor version byte, a count, then (level id,
/// offset in ms as an int16) pairs. A map at zero has no entry.
/// </summary>
public sealed class MapOffsetSettings
{
    public const int MaxMs = 300;

    private const string DefaultPath = "user://map-offsets.xmo";
    private const byte MajorVersion = 0;
    private const byte MinorVersion = 1;
    private static readonly byte[] Magic = "XMO"u8.ToArray();

    private readonly string? _path;
    private readonly Dictionary<string, short> _offsets = new(StringComparer.Ordinal);

    public MapOffsetSettings(string? path = null, bool persist = true)
    {
        _path = persist ? path ?? Xanadu.Storage.GamePaths.Globalize(DefaultPath) : null;
        Load();
    }

    public long Get(string levelId) =>
        _offsets.TryGetValue(levelId, out short offset) ? offset : 0;

    /// <summary>Sets a map's offset, clamped, and returns what it became.</summary>
    public long Set(string levelId, long offsetMs)
    {
        short clamped = (short)Math.Clamp(offsetMs, -MaxMs, MaxMs);
        if (clamped == 0)
            _offsets.Remove(levelId);
        else
            _offsets[levelId] = clamped;

        Save();
        return clamped;
    }

    private void Load()
    {
        if (_path is null || !File.Exists(_path))
            return;

        try
        {
            using FileStream stream = File.OpenRead(_path);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic) || reader.ReadByte() != MajorVersion)
                return;
            reader.ReadByte();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string id = reader.ReadString();
                _offsets[id] = (short)Math.Clamp((int)reader.ReadInt16(), -MaxMs, MaxMs);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A damaged file costs the player their map offsets, not their game.
            _offsets.Clear();
        }
    }

    private void Save()
    {
        if (_path is null)
            return;

        try
        {
            string temporary = _path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(MajorVersion);
                writer.Write(MinorVersion);
                writer.Write(_offsets.Count);
                foreach ((string id, short offset) in _offsets)
                {
                    writer.Write(id);
                    writer.Write(offset);
                }
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Xanadu.Diagnostics.GameLog.Warning($"Could not save map offsets: {exception.Message}");
        }
    }
}
