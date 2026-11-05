using System.Collections.Generic;
using System.IO;
using System.Text;
using Xanadu.Storage;

namespace Xanadu.Levels;

/// <summary>
/// Text placed in a level, in an optional chunk of its own: maps without text
/// leave it out, and readers that predate it skip it. Each text's turn came
/// later, and trails the chunk as one list, so older files read as upright.
/// </summary>
public static partial class XmfBinary
{
    /// <summary>The longest text one item may hold, in characters.</summary>
    public const int MaxTextLength = 4_096;

    internal static bool HasTexts(IChartItems items) => items.Texts is { Count: > 0 };

    internal static byte[] WriteTexts(List<TextDocument> texts)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteTexts(writer, texts);
        WriteTextRotations(writer, texts);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteTexts(BinaryWriter writer, List<TextDocument> texts)
    {
        writer.Write((uint)texts.Count);
        foreach (TextDocument text in texts)
        {
            WriteGroups(writer, text.Groups);
            writer.Write(text.Shader);
            writer.Write(text.X);
            writer.Write(text.Y);
            writer.Write(text.Size);
            writer.Write(text.SelfModulate);
            writer.Write(Clip(text.Text));
        }
    }

    internal static List<TextDocument> ReadTexts(Chunk chunk)
    {
        using BinaryReader reader = chunk.OpenReader();
        List<TextDocument> texts = ReadTexts(reader);
        ReadTextRotations(reader, texts);
        return texts;
    }

    internal static void WriteTextRotations(BinaryWriter writer, List<TextDocument> texts)
    {
        foreach (TextDocument text in texts)
            writer.Write(text.Rotation);
    }

    /// <summary>Reads the trailing turns when the chunk has them; texts from older files stay upright.</summary>
    internal static void ReadTextRotations(BinaryReader reader, List<TextDocument> texts)
    {
        if (reader.BaseStream.Length - reader.BaseStream.Position < (long)texts.Count * sizeof(float))
            return;
        foreach (TextDocument text in texts)
            text.Rotation = reader.ReadSingle();
    }

    internal static List<TextDocument> ReadTexts(BinaryReader reader)
    {
        uint count = ReadCount(reader, MaxItems, "text");
        var texts = new List<TextDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            texts.Add(new TextDocument
            {
                Groups = ReadGroups(reader),
                Shader = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Size = reader.ReadSingle(),
                SelfModulate = reader.ReadUInt32(),
                Text = Clip(reader.ReadString()),
            });
        }
        return texts;
    }

    private static string Clip(string? text) =>
        text is null ? "" : text.Length > MaxTextLength ? text[..MaxTextLength] : text;
}
