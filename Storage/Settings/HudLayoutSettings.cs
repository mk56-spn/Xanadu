using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Core.Diagnostics;
using Xanadu.Core.Storage;

namespace Xanadu.Storage.Settings;

/// <summary>
/// The player's play HUD layout: per element, where it is anchored on the
/// screen and which of its points sits there, its offset from that anchor, its
/// scale, turn, mirroring and whether it shows. An element not saved keeps its
/// built-in layout. The .xhl format is little-endian binary: <c>"XHL"</c>, a
/// major and a minor version byte, a count, then per element its id, anchor and
/// origin (0–8, row by row), offset x and y in pixels, scale, rotation in
/// radians, a flags byte (1 shown, 2 mirrored across, 4 mirrored down).
/// </summary>
public sealed class HudLayoutSettings
{
    private const string DefaultPath = "user://hud-layout.xhl";
    private const byte MajorVersion = 1;
    private const byte MinorVersion = 0;
    private static readonly byte[] Magic = "XHL"u8.ToArray();

    private readonly string? _path;
    private readonly Dictionary<string, HudLayoutEntry> _items = new(StringComparer.Ordinal);

    public HudLayoutSettings(string? path = null, bool persist = true)
    {
        _path = persist ? path ?? GamePaths.Full(DefaultPath) : null;
        Load();
    }

    public bool TryGet(string id, out HudLayoutEntry entry) =>
        _items.TryGetValue(id, out entry);

    /// <summary>Sets an element's layout; nothing is written until <see cref="Save"/>.</summary>
    public void Set(string id, HudLayoutEntry entry) => _items[id] = entry;

    /// <summary>Forgets an element's layout, so it goes back to its built-in one.</summary>
    public void Remove(string id) => _items.Remove(id);

    public readonly record struct HudLayoutEntry(
        byte Anchor,
        byte Origin,
        float X,
        float Y,
        float Scale,
        float Rotation,
        bool Visible,
        bool FlipX,
        bool FlipY);

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
                byte anchor = Math.Min(reader.ReadByte(), (byte)8);
                byte origin = Math.Min(reader.ReadByte(), (byte)8);
                float x = reader.ReadSingle(), y = reader.ReadSingle();
                float scale = Math.Clamp(reader.ReadSingle(), 0.1f, 10f);
                float rotation = reader.ReadSingle();
                byte flags = reader.ReadByte();
                if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(rotation))
                    continue;
                _items[id] = new HudLayoutEntry(anchor, origin, x, y, scale, rotation, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or FormatException or EndOfStreamException)
        {
            // A damaged file costs the player their HUD layout, not their game.
            _items.Clear();
        }
    }

    public void Save()
    {
        if (_path is null)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = _path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(MajorVersion);
                writer.Write(MinorVersion);
                writer.Write(_items.Count);
                foreach ((string id, HudLayoutEntry entry) in _items)
                {
                    writer.Write(id);
                    writer.Write(entry.Anchor);
                    writer.Write(entry.Origin);
                    writer.Write(entry.X);
                    writer.Write(entry.Y);
                    writer.Write(entry.Scale);
                    writer.Write(entry.Rotation);
                    writer.Write((byte)((entry.Visible ? 1 : 0) | (entry.FlipX ? 2 : 0) | (entry.FlipY ? 4 : 0)));
                }
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GameLog.Warning($"Could not save the HUD layout: {exception.Message}");
        }
    }
}
