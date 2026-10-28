using System.Collections.Generic;

namespace Xanadu.Items;

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
    public XifShaderScope Scope { get; set; } = XifShaderScope.Map;

    /// <summary>The owning map or mapset's shader slot, for <see cref="XifShaderScope.Map"/>. 0 means none.</summary>
    public int Slot { get; set; }

    /// <summary>The common shader's resource path, for <see cref="XifShaderScope.Common"/>.</summary>
    public string Path { get; set; } = "";

    /// <summary>Whether this is the "no shader" value: a map ref to slot 0.</summary>
    public bool IsNone => Scope == XifShaderScope.Map && Slot == 0;

    public static XifShaderRef OfMap(int slot) => new() { Scope = XifShaderScope.Map, Slot = slot };

    public static XifShaderRef OfCommon(string path) => new() { Scope = XifShaderScope.Common, Path = path };
}

/// <summary>
/// One visual primitive a Xif lists itself as built from, and the shader it
/// is drawn with, if any. A manifest entry only; drawing it is up to
/// whatever embeds the Xif.
/// </summary>
public sealed class XifPrimitive
{
    public string Kind { get; set; } = "";

    public string Ref { get; set; } = "";

    /// <summary>The shader this layer is drawn with; <see cref="XifShaderRef.IsNone"/> when it has none.</summary>
    public XifShaderRef Shader { get; set; } = new();
}

/// <summary>
/// A self-contained item: the primitives and shaders it uses, and the params
/// tuned for it. A note is a Xif plus note-specific fields (time, target,
/// kind, ...); a rig attachment embeds a Xif the same way a note does. Xif
/// only lists what it is built from and points at its shaders by reference —
/// drawing, animating, and resolving those shaders stays with whatever
/// embeds it.
/// </summary>
public sealed class Xif
{
    /// <summary>Stable identity once this item is promoted to a standalone asset; empty while it is only inline data.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Whether this item's data lives with the level that placed it, or the game.</summary>
    public XifOwner Owner { get; set; } = XifOwner.Level;

    public List<XifPrimitive> Primitives { get; set; } = new();

    /// <summary>The shaders this item uses, by reference.</summary>
    public List<XifShaderRef> Shaders { get; set; } = new();

    /// <summary>Tuned uniform values by name; vectors and colours are one float per channel.</summary>
    public Dictionary<string, float[]> Params { get; set; } = new();

    /// <summary>A Xif that uses a single map-owned shader slot, or none for slot 0.</summary>
    public static Xif ForShader(int slot) => slot == 0 ? new Xif() : new Xif { Shaders = { XifShaderRef.OfMap(slot) } };

    /// <summary>A Xif that uses a single common shader.</summary>
    public static Xif ForCommonShader(string path) => new() { Shaders = { XifShaderRef.OfCommon(path) } };

    /// <summary>A Xif for the shaders an ECS entity carries at the editor/ECS boundary.</summary>
    public static Xif FromEcs(int mapSlot, string commonPath)
    {
        var item = new Xif();
        if (mapSlot != 0)
            item.Shaders.Add(XifShaderRef.OfMap(mapSlot));
        if (!string.IsNullOrEmpty(commonPath))
            item.Shaders.Add(XifShaderRef.OfCommon(commonPath));
        return item;
    }

    /// <summary>
    /// The map shader slot this item uses at the editor/ECS boundary, or 0 if
    /// it uses none there (including if it only uses common shaders).
    /// </summary>
    public int PrimaryShader => Shaders.Find(shader => shader.Scope == XifShaderScope.Map)?.Slot ?? 0;

    /// <summary>
    /// The common shader this item uses at the editor/ECS boundary, or "" if
    /// it uses none there (including if it only uses map shaders).
    /// </summary>
    public string PrimaryCommonShader => Shaders.Find(shader => shader.Scope == XifShaderScope.Common)?.Path ?? "";
}
