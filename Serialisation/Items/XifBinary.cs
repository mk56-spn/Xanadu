using System.Collections.Generic;
using System.IO;
using Xanadu.Core.Items;
using Xanadu.Core.Storage;

namespace Xanadu.Serialisation.Items;

/// <summary>
/// The binary form of a <see cref="Xif"/> tree inside a chunked file. The tree
/// is stored flat, depth first: a node count, then each node with its parent's
/// index (-1 for the root), its identity, and its modules, each a tag byte
/// followed by that module's fields.
/// </summary>
public static partial class XifBinary
{
    private const uint MaxNodes = 65_536;
    private const uint MaxModules = 64;
    private const uint MaxPolygons = 1_000_000;
    private const uint MaxParams = 4_096;
    private const uint MaxLayers = 64;
    private const uint MaxChannels = 16;
    private const uint MaxPoints = 4_096;

    /// <summary>Which module follows; a reader refuses a tag it does not know.</summary>
    private enum ModuleTag : byte
    {
        Geometry = 1,
        Shader = 2,
        Skeleton = 3,
        Clips = 4,
        Attachment = 5,
        Variants = 6,
        Particles = 7,
        Mesh = 8,
        Chain = 9,
        Cloth = 10,
        Instancer = 11,

        /// <summary>Geometry whose shapes each carry their kind (free, quad, ellipse) after their fill.</summary>
        ShapedGeometry = 12,

        /// <summary>A shader module with its fit flag ahead of the rest.</summary>
        FittedShader = 13,

        /// <summary>Alternate art whose shapes each carry their kind, as <see cref="ShapedGeometry"/> does.</summary>
        ShapedVariants = 14,

        /// <summary>Clips whose channels are addressed by node, part and field (Core/Keys), in place of <see cref="Clips"/>.</summary>
        KeyedClips = 15,

        /// <summary>A fitted shader module with the layers stacked over it after its ref.</summary>
        LayeredShader = 16,
    }

    /// <summary>Every string a tree's entry carries, so it ends up in the shared table.</summary>
    public static void Index(Xif item, StringTable strings)
    {
        strings.Add(item.Id);
        strings.Add(item.Name);
        strings.Add(item.Ref);
        foreach (XifModule module in item.Modules)
        {
            IndexRig(module, strings);
            if (module is not XifShader shader)
                continue;
            strings.Add(shader.Shader.Path);
            foreach (string layer in shader.Layers)
                strings.Add(layer);
            foreach (string key in shader.Params.Keys)
                strings.Add(key);
            foreach (KeyValuePair<string, string> texture in shader.Textures)
            {
                strings.Add(texture.Key);
                strings.Add(texture.Value);
            }
        }
        foreach (Xif child in item.Children)
            Index(child, strings);
    }

    public static void Write(BinaryWriter writer, Xif item, StringTable strings)
    {
        var nodes = new List<(Xif Node, int Parent)>();
        Flatten(item, -1, nodes);
        writer.Write((uint)nodes.Count);
        foreach ((Xif node, int parent) in nodes)
            WriteNode(writer, node, parent, strings);
    }

    public static Xif Read(BinaryReader reader, StringTable strings)
    {
        uint count = ReadCount(reader, MaxNodes, "xif node");
        if (count == 0)
            throw new FormatException("A xif tree needs a root node.");

        var nodes = new List<Xif>((int)count);
        for (uint i = 0; i < count; i++)
        {
            int parent = reader.ReadInt32();
            Xif node = ReadNode(reader, strings);
            if (i == 0)
            {
                if (parent != -1)
                    throw new FormatException("A xif root has no parent.");
            }
            else
            {
                if (parent < 0 || parent >= nodes.Count)
                    throw new FormatException($"Xif node {i} has parent {parent}, which does not come before it.");
                nodes[parent].Children.Add(node);
            }
            nodes.Add(node);
        }
        return nodes[0];
    }

    private static void Flatten(Xif node, int parent, List<(Xif, int)> nodes)
    {
        int index = nodes.Count;
        nodes.Add((node, parent));
        foreach (Xif child in node.Children)
            Flatten(child, index, nodes);
    }

    private static void WriteNode(BinaryWriter writer, Xif node, int parent, StringTable strings)
    {
        writer.Write(parent);
        strings.WriteIndex(writer, node.Id);
        strings.WriteIndex(writer, node.Name);
        writer.Write((byte)node.Owner);
        strings.WriteIndex(writer, node.Ref);

        writer.Write((uint)node.Modules.Count);
        foreach (XifModule module in node.Modules)
        {
            switch (module)
            {
                case XifGeometry geometry:
                    writer.Write((byte)ModuleTag.ShapedGeometry);
                    WriteGeometry(writer, geometry);
                    break;
                case XifShader shader:
                    writer.Write((byte)ModuleTag.LayeredShader);
                    WriteShader(writer, shader, strings);
                    break;
                case XifInstancer instancer:
                    writer.Write((byte)ModuleTag.Instancer);
                    WriteInstancer(writer, instancer);
                    break;
                default:
                    if (!TryWriteRig(writer, module, strings))
                        throw new InvalidOperationException($"No binary form for xif module {module.GetType().Name}.");
                    break;
            }
        }
    }

    private static Xif ReadNode(BinaryReader reader, StringTable strings)
    {
        var node = new Xif
        {
            Id = strings.ReadIndex(reader),
            Name = strings.ReadIndex(reader),
            Owner = ReadEnum<XifOwner>(reader),
            Ref = strings.ReadIndex(reader),
        };

        uint modules = ReadCount(reader, MaxModules, "xif module");
        for (uint i = 0; i < modules; i++)
        {
            node.Modules.Add(ReadEnum<ModuleTag>(reader) switch
            {
                ModuleTag.Geometry => ReadGeometry(reader, kinds: false),
                ModuleTag.ShapedGeometry => ReadGeometry(reader, kinds: true),
                ModuleTag.Shader => ReadShader(reader, strings, fit: false),
                ModuleTag.FittedShader => ReadShader(reader, strings, fit: true),
                ModuleTag.LayeredShader => ReadShader(reader, strings, fit: true, layers: true),
                ModuleTag.Instancer => ReadInstancer(reader),
                var tag => ReadRig(reader, tag, strings),
            });
        }
        return node;
    }

    private static void WriteGeometry(BinaryWriter writer, XifGeometry geometry)
    {
        writer.Write((uint)geometry.Polygons.Count);
        foreach (XifPolygon polygon in geometry.Polygons)
        {
            writer.Write((uint)polygon.Points.Count);
            foreach (Vector2 point in polygon.Points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
            writer.Write(polygon.Fill.R);
            writer.Write(polygon.Fill.G);
            writer.Write(polygon.Fill.B);
            writer.Write(polygon.Fill.A);
            writer.Write((byte)polygon.Kind);
        }
    }

    private static XifGeometry ReadGeometry(BinaryReader reader, bool kinds)
    {
        var geometry = new XifGeometry();
        uint count = ReadCount(reader, MaxPolygons, "xif polygon");
        for (uint p = 0; p < count; p++)
        {
            uint points = ReadCount(reader, MaxPoints, "xif point");
            var polygon = new XifPolygon { Points = new List<Vector2>((int)points) };
            for (uint i = 0; i < points; i++)
                polygon.Points.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            polygon.Fill = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (kinds)
                polygon.Kind = ReadEnum<XifShapeKind>(reader);
            geometry.Polygons.Add(polygon);
        }
        return geometry;
    }

    private static void WriteShader(BinaryWriter writer, XifShader shader, StringTable strings)
    {
        writer.Write(shader.Fit);
        writer.Write((byte)shader.Shader.Scope);
        writer.Write(shader.Shader.Slot);
        strings.WriteIndex(writer, shader.Shader.Path);
        writer.Write((uint)shader.Layers.Count);
        foreach (string layer in shader.Layers)
            strings.WriteIndex(writer, layer);

        writer.Write((uint)shader.Params.Count);
        foreach (KeyValuePair<string, float[]> param in shader.Params)
        {
            strings.WriteIndex(writer, param.Key);
            writer.Write((uint)param.Value.Length);
            foreach (float channel in param.Value)
                writer.Write(channel);
        }

        writer.Write((uint)shader.Textures.Count);
        foreach (KeyValuePair<string, string> texture in shader.Textures)
        {
            strings.WriteIndex(writer, texture.Key);
            strings.WriteIndex(writer, texture.Value);
        }
    }

    private static XifShader ReadShader(BinaryReader reader, StringTable strings, bool fit, bool layers = false)
    {
        bool fitted = fit && reader.ReadBoolean();
        var shader = new XifShader
        {
            Fit = fitted,
            Shader = new XifShaderRef
            {
                Scope = ReadEnum<XifShaderScope>(reader),
                Slot = reader.ReadInt32(),
                Path = strings.ReadIndex(reader),
            },
        };

        if (layers)
        {
            uint layerCount = ReadCount(reader, MaxLayers, "xif shader layer");
            for (uint i = 0; i < layerCount; i++)
                shader.Layers.Add(strings.ReadIndex(reader));
        }

        uint paramCount = ReadCount(reader, MaxParams, "xif param");
        for (uint i = 0; i < paramCount; i++)
        {
            string key = strings.ReadIndex(reader);
            uint channelCount = ReadCount(reader, MaxChannels, "xif param channel");
            var channels = new float[channelCount];
            for (uint c = 0; c < channelCount; c++)
                channels[c] = reader.ReadSingle();
            shader.Params[key] = channels;
        }

        uint textureCount = ReadCount(reader, MaxParams, "xif texture");
        for (uint i = 0; i < textureCount; i++)
            shader.Textures[strings.ReadIndex(reader)] = strings.ReadIndex(reader);
        return shader;
    }

    private static void WriteInstancer(BinaryWriter writer, XifInstancer instancer)
    {
        writer.Write(instancer.Count);
        writer.Write(instancer.Area.X);
        writer.Write(instancer.Area.Y);
        writer.Write(instancer.ScaleJitter);
        writer.Write(instancer.TurnJitter);
        writer.Write(instancer.Seed);
    }

    private static XifInstancer ReadInstancer(BinaryReader reader)
    {
        var instancer = new XifInstancer
        {
            Count = reader.ReadInt32(),
            Area = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            ScaleJitter = reader.ReadSingle(),
            TurnJitter = reader.ReadSingle(),
            Seed = reader.ReadUInt32(),
        };
        if (instancer.Count < 0 || instancer.Count > XifInstancer.MaxCount)
            throw new FormatException($"Xif instancer count {instancer.Count} is out of range.");
        if (!float.IsFinite(instancer.Area.X) || !float.IsFinite(instancer.Area.Y) ||
            !float.IsFinite(instancer.ScaleJitter) || !float.IsFinite(instancer.TurnJitter))
            throw new FormatException("Xif instancer values are not finite.");
        return instancer;
    }

    private static uint ReadCount(BinaryReader reader, uint max, string what)
    {
        uint count = reader.ReadUInt32();
        if (count > max)
            throw new FormatException($"Too many {what} entries ({count}).");
        return count;
    }

    private static T ReadEnum<T>(BinaryReader reader) where T : struct, Enum
    {
        byte value = reader.ReadByte();
        T result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw new FormatException($"Unknown {typeof(T).Name} value {value}.");
        return result;
    }
}
