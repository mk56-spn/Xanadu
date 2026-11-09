using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Xanadu.Storage;

namespace Xanadu.Particles;

/// <summary>A particle library file that cannot be read.</summary>
public sealed class XpfFormatException(string message) : Exception(message);

/// <summary>Binary XPF1 reader/writer for a <see cref="XpfLibrary"/>, on the shared chunked frame.</summary>
public static class XpfBinary
{
    private const string Format = "XPF";

    private static readonly byte[] Magic = "XPF1"u8.ToArray();

    private enum ChunkId : uint
    {
        Strings = 1,
        Particles = 2,
    }

    public static void Save(string path, XpfLibrary library) => BinaryFile.Save(path, Write(library));

    public static XpfLibrary Load(string path) => Read(File.ReadAllBytes(path));

    public static byte[] Write(XpfLibrary library)
    {
        string? problem = XpfValidation.Problem(library);
        if (problem is not null)
            throw new XpfFormatException(problem);

        var strings = new StringTable(Format);
        foreach (XpfParticle particle in library.Particles)
            strings.Add(particle.Name);

        var file = new ChunkWriter(Magic, XpfLibrary.MajorVersion, XpfLibrary.MinorVersion);
        file.Add((uint)ChunkId.Strings, strings.Write());
        file.Add((uint)ChunkId.Particles, WriteParticles(library.Particles, strings));
        return file.ToArray();
    }

    public static XpfLibrary Read(byte[] data)
    {
        try
        {
            ChunkReader file = ChunkReader.Open(data, Magic, XpfLibrary.MajorVersion, Format);
            StringTable strings = StringTable.Read(file.Get((uint)ChunkId.Strings, Format), Format);
            var library = new XpfLibrary
            {
                Particles = ReadParticles(file.Get((uint)ChunkId.Particles, Format), strings),
            };

            string? problem = XpfValidation.Problem(library);
            return problem is null ? library : throw new XpfFormatException(problem);
        }
        catch (Exception exception) when (exception is FormatException or EndOfStreamException)
        {
            throw new XpfFormatException(exception.Message);
        }
    }

    private static byte[] WriteParticles(List<XpfParticle> particles, StringTable strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)particles.Count);
        foreach (XpfParticle particle in particles)
        {
            strings.WriteIndex(writer, particle.Name);
            writer.Write((byte)particle.Texture);
            writer.Write(particle.Amount);
            writer.Write(particle.Lifetime);
            writer.Write(particle.Explosiveness);
            writer.Write(particle.Randomness);
            writer.Write((byte)particle.Shape);
            WriteVector(writer, particle.BoxSize);
            writer.Write(particle.Direction);
            writer.Write(particle.Spread);
            writer.Write(particle.HeadedSpread);
            writer.Write(particle.SpeedMin);
            writer.Write(particle.SpeedMax);
            WriteVector(writer, particle.Gravity);
            writer.Write(particle.DampingMin);
            writer.Write(particle.DampingMax);
            writer.Write(particle.ScaleMin);
            writer.Write(particle.ScaleMax);
            writer.Write((byte)particle.ScaleCurve.Count);
            foreach (XpfCurvePoint point in particle.ScaleCurve)
            {
                writer.Write(point.Time);
                writer.Write(point.Value);
            }

            writer.Write(particle.AngleMin);
            writer.Write(particle.AngleMax);
            writer.Write(particle.Upright);
            WriteColor(writer, particle.Color);
            writer.Write((byte)particle.Ramp.Count);
            foreach (XpfColorStop stop in particle.Ramp)
            {
                writer.Write(stop.Time);
                WriteColor(writer, stop.Color);
            }

            writer.Write(particle.Reach);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static List<XpfParticle> ReadParticles(Chunk chunk, StringTable strings)
    {
        using BinaryReader reader = chunk.OpenReader();
        uint count = reader.ReadUInt32();
        if (count > XpfValidation.MaxParticles)
            throw new FormatException("Too many XPF particles.");

        var particles = new List<XpfParticle>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var particle = new XpfParticle
            {
                Name = strings.ReadIndex(reader),
                Texture = (XpfTexture)reader.ReadByte(),
                Amount = reader.ReadInt32(),
                Lifetime = reader.ReadSingle(),
                Explosiveness = reader.ReadSingle(),
                Randomness = reader.ReadSingle(),
                Shape = (XpfShape)reader.ReadByte(),
                BoxSize = ReadVector(reader),
                Direction = reader.ReadSingle(),
                Spread = reader.ReadSingle(),
                HeadedSpread = reader.ReadSingle(),
                SpeedMin = reader.ReadSingle(),
                SpeedMax = reader.ReadSingle(),
                Gravity = ReadVector(reader),
                DampingMin = reader.ReadSingle(),
                DampingMax = reader.ReadSingle(),
                ScaleMin = reader.ReadSingle(),
                ScaleMax = reader.ReadSingle(),
            };

            int points = reader.ReadByte();
            for (int p = 0; p < points; p++)
                particle.ScaleCurve.Add(new XpfCurvePoint(reader.ReadSingle(), reader.ReadSingle()));

            particle.AngleMin = reader.ReadSingle();
            particle.AngleMax = reader.ReadSingle();
            particle.Upright = reader.ReadBoolean();
            particle.Color = ReadColor(reader);
            int stops = reader.ReadByte();
            for (int s = 0; s < stops; s++)
                particle.Ramp.Add(new XpfColorStop(reader.ReadSingle(), ReadColor(reader)));

            particle.Reach = reader.ReadSingle();
            particles.Add(particle);
        }

        return particles;
    }

    private static void WriteVector(BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    private static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    private static void WriteColor(BinaryWriter writer, Color color)
    {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
        writer.Write(color.A);
    }

    private static Color ReadColor(BinaryReader reader) =>
        new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}
