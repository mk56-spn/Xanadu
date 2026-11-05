using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Layering;

namespace Xanadu.Levels;

/// <summary>
/// The layer table chunk of an XMF file (docs/render-architecture.md step 5):
/// each layer that has a name or a pass, with the pass shader's keys. Left out
/// when no layer has settings; a file without it reads with every layer plain.
/// </summary>
internal static class XmfLayerTable
{
    /// <summary>More entries than there are layers can't be right.</summary>
    private const uint MaxLayers = DrawOrder.MaxLayer * 2;

    private const int MaxName = 256;

    internal static bool HasLayers(IChartItems items) => items.Layers is { Count: > 0 };

    internal static byte[] Write(IChartItems items)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        Write(writer, items);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void Write(BinaryWriter writer, IChartItems items)
    {
        List<LayerDocument> layers = items.Layers ?? new List<LayerDocument>();
        writer.Write((uint)layers.Count);
        foreach (LayerDocument layer in layers.OrderBy(layer => layer.Number))
        {
            writer.Write(layer.Number);
            writer.Write(Clip(layer.Name));
            writer.Write((byte)layer.Pass);
            writer.Write(layer.PassShader);
            XmfShaderKeys.Write(writer, layer.PassKeys ?? new List<ShaderKeyDocument>());
        }
    }

    internal static void Read(BinaryReader reader, IChartItems items)
    {
        uint count = XmfBinary.ReadCount(reader, MaxLayers, "layer");
        var layers = new List<LayerDocument>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var layer = new LayerDocument
            {
                Number = reader.ReadInt32(),
                Name = reader.ReadString(),
                Pass = ReadPass(reader.ReadByte()),
                PassShader = reader.ReadInt32(),
            };

            XmfShaderKeys.Read(reader, layer.PassKeys);

            layers.Add(layer);
        }

        items.Layers = layers;
    }

    private static LayerPassKind ReadPass(byte value) =>
        Enum.IsDefined((LayerPassKind)value)
            ? (LayerPassKind)value
            : throw new FormatException("XMF layer has an unknown pass kind.");

    private static string Clip(string? text) =>
        text is null ? "" : text.Length > MaxName ? text[..MaxName] : text;
}
