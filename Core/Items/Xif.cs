using System.Collections.Generic;

namespace Xanadu.Core.Items;

/// <summary>Who holds a Xif's data: the level that placed it, or the game's shared asset store.</summary>
public enum XifOwner
{
    Level,
    Game,
}

/// <summary>Where a shader a Xif points at lives.</summary>
public enum XifShaderScope
{
    /// <summary>A slot in the shader pool owned by whatever map or mapset placed this item.</summary>
    Map,

    /// <summary>A shader every editor and animator may use, independent of any one map.</summary>
    Common,
}

/// <summary>One shader a Xif points at, by reference rather than by owning its source.</summary>
public sealed class XifShaderRef
{
    public XifShaderScope Scope { get; init; } = XifShaderScope.Map;

    /// <summary>The owning map or mapset's shader slot, for <see cref="XifShaderScope.Map"/>. 0 means none.</summary>
    public int Slot { get; init; }

    /// <summary>The common shader's resource path, for <see cref="XifShaderScope.Common"/>.</summary>
    public string Path { get; init; } = "";

    /// <summary>Whether this is the "no shader" value: a map ref to slot 0.</summary>
    public bool IsNone => Scope == XifShaderScope.Map && Slot == 0;

    public static XifShaderRef OfMap(int slot) => new() { Scope = XifShaderScope.Map, Slot = slot };

    public static XifShaderRef OfCommon(string path) => new() { Scope = XifShaderScope.Common, Path = path };

    public XifShaderRef Copy() => new() { Scope = Scope, Slot = Slot, Path = Path };

    public bool Same(XifShaderRef other) => Scope == other.Scope && Slot == other.Slot && Path == other.Path;
}

/// <summary>
/// One part of what a <see cref="Xif"/> is: its shape, its shader, its bones,
/// its clips. A Xif is whatever its modules make it; there is no per-kind item
/// type. Each module copies and compares itself so undo and copy stay generic.
/// </summary>
public abstract class XifModule
{
    public abstract XifModule Copy();

    /// <summary>Whether <paramref name="other"/> is the same kind of module holding the same values.</summary>
    public abstract bool Same(XifModule other);
}

/// <summary>A filled outline in the item's own space, in order.</summary>
/// <summary>
/// What a shape is: a free outline of points, or a primitive the editor keeps
/// as such. Every straight kind keeps its outline in <see cref="XifPolygon.Points"/>,
/// so drawing never needs to know and the kind only changes how it is edited;
/// a curve's points are its on-curve and control points, which drawing reads as such.
/// </summary>
public enum XifShapeKind : byte
{
    /// <summary>Points placed one by one.</summary>
    Free,

    /// <summary>An axis-aligned rectangle: its four corners, dragged as a box.</summary>
    Quad,

    /// <summary>An axis-aligned ellipse, sized by its four sides.</summary>
    Ellipse,

    /// <summary>
    /// A closed run of quadratic Bézier segments. <see cref="XifPolygon.Points"/>
    /// alternate on-curve and control points: point 2i is on the curve, 2i+1
    /// pulls the segment from 2i to 2i+2 (wrapping). The curves are filled on
    /// the GPU; nothing samples them into points (see <c>ArtMesh</c>).
    /// </summary>
    Curve,
}

public sealed class XifPolygon
{
    public List<Vector2> Points { get; init; } = [];

    public XifShapeKind Kind { get; set; } = XifShapeKind.Free;

    /// <summary>The polygon's colour; the node's shader may tint it further.</summary>
    public Color Fill { get; set; } = Colors.White;

    public XifPolygon Copy() => new() { Points = [.. Points], Fill = Fill, Kind = Kind };

    public bool Same(XifPolygon other)
    {
        if (Fill != other.Fill || Kind != other.Kind || Points.Count != other.Points.Count)
            return false;
        for (int i = 0; i < Points.Count; i++)
        {
            if (Points[i] != other.Points[i])
                return false;
        }
        return true;
    }
}

/// <summary>The node's shape: polygons only, no textures (a texture is a shader input).</summary>
public sealed class XifGeometry : XifModule
{
    public List<XifPolygon> Polygons { get; set; } = [];

    public override XifModule Copy()
    {
        var copy = new XifGeometry();
        foreach (XifPolygon polygon in Polygons)
            copy.Polygons.Add(polygon.Copy());
        return copy;
    }

    public override bool Same(XifModule other)
    {
        if (other is not XifGeometry geometry || geometry.Polygons.Count != Polygons.Count)
            return false;
        for (int i = 0; i < Polygons.Count; i++)
        {
            if (!Polygons[i].Same(geometry.Polygons[i]))
                return false;
        }
        return true;
    }
}

/// <summary>
/// How the node looks: the shader its geometry is drawn with and everything
/// fed into it, tuned params and textures alike. More common shaders can be
/// stacked over it (<see cref="Layers"/>), stitched into one on the GPU: the
/// shader mirror of a Xif's modules (Docs/xif-modules.md).
/// </summary>
public sealed class XifShader : XifModule
{
    public XifShaderRef Shader { get; set; } = new();

    /// <summary>
    /// Common shaders stacked over <see cref="Shader"/>, bottom to top, by resource path:
    /// each one's deform runs on what the one under it moved, and its shade gets the colour
    /// so far as <c>i.colour</c>. Their params share <see cref="Params"/>, by name.
    /// </summary>
    public List<string> Layers { get; init; } = [];

    /// <summary>Tuned uniform values by name; vectors and colours are one float per channel.</summary>
    public Dictionary<string, float[]> Params { get; init; } = [];

    /// <summary>Texture inputs by sampler name, as resource paths.</summary>
    public Dictionary<string, string> Textures { get; set; } = [];

    /// <summary>
    /// The shader fits itself to the node's shape: its <c>[Fit]</c> values
    /// take their x and y from the shape's half size (a flare's box), not
    /// from <see cref="Params"/>.
    /// </summary>
    public bool Fit { get; set; }

    public override XifModule Copy()
    {
        var copy = new XifShader { Shader = Shader.Copy(), Fit = Fit, Layers = [.. Layers] };
        foreach (KeyValuePair<string, float[]> param in Params)
            copy.Params[param.Key] = (float[])param.Value.Clone();
        foreach (KeyValuePair<string, string> texture in Textures)
            copy.Textures[texture.Key] = texture.Value;
        return copy;
    }

    public override bool Same(XifModule other)
    {
        if (other is not XifShader shader || !Shader.Same(shader.Shader) || Fit != shader.Fit ||
            Params.Count != shader.Params.Count || Textures.Count != shader.Textures.Count ||
            !System.Linq.Enumerable.SequenceEqual(Layers, shader.Layers))
            return false;
        foreach (KeyValuePair<string, float[]> param in Params)
        {
            if (!shader.Params.TryGetValue(param.Key, out float[]? values) ||
                !System.MemoryExtensions.SequenceEqual<float>(param.Value, values))
                return false;
        }
        foreach (KeyValuePair<string, string> texture in Textures)
        {
            if (!shader.Textures.TryGetValue(texture.Key, out string? path) || path != texture.Value)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Anything composable: a note, a flare, a body mesh, a hair chain, a whole
/// character. What it is comes from its <see cref="Modules"/>; it may hold
/// child Xifs. A node is inline, or points at a promoted Xif by
/// <see cref="Ref"/>. Drawing, animating and resolving shaders stay with
/// whatever plays it. See Docs/xif-modules.md.
/// </summary>
public sealed class Xif
{
    /// <summary>Stable identity once this item is promoted to a standalone asset; empty while it is only inline data.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Whether this item's data lives with the level that placed it, or the game.</summary>
    public XifOwner Owner { get; init; } = XifOwner.Level;

    /// <summary>The <see cref="Id"/> of a promoted Xif this node stands for; empty for an inline node.</summary>
    public string Ref { get; init; } = "";

    /// <summary>At most one module of each type.</summary>
    public List<XifModule> Modules { get; set; } = [];

    public List<Xif> Children { get; set; } = [];

    public bool IsReference => Ref.Length > 0;

    /// <summary>This node's module of type <typeparamref name="T"/>, or null.</summary>
    public T? Get<T>() where T : XifModule
    {
        foreach (XifModule module in Modules)
        {
            if (module is T found)
                return found;
        }
        return null;
    }

    /// <summary>This node's module of type <typeparamref name="T"/>, added empty if it has none.</summary>
    public T Need<T>() where T : XifModule, new()
    {
        T? found = Get<T>();
        if (found is not null)
            return found;
        var added = new T();
        Modules.Add(added);
        return added;
    }

    /// <summary>The node's polygons, empty if it has no geometry.</summary>
    public IReadOnlyList<XifPolygon> Polygons => Get<XifGeometry>()?.Polygons ?? (IReadOnlyList<XifPolygon>)[];

    /// <summary>The node's shader ref, the none value if it has no shader module.</summary>
    public XifShaderRef ShaderRef => Get<XifShader>()?.Shader ?? new XifShaderRef();

    /// <summary>A node drawn with one common shader.</summary>
    public static Xif ForCommonShader(string path) => new() { Modules = { new XifShader { Shader = XifShaderRef.OfCommon(path) } } };
}
