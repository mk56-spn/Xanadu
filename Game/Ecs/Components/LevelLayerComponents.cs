using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Timeline;

namespace Xanadu.Ecs;

/// <summary>
/// What a layer's full-screen pass reads (Docs/render-architecture.md D8):
/// nothing, everything drawn so far, or only the layer's own content.
/// </summary>
public enum LayerPassKind : byte
{
    None,

    /// <summary>Reads the whole screen drawn so far.</summary>
    BackgroundReader,

    /// <summary>Reads only the layer's own content, drawn into its own offscreen target.</summary>
    SelfContained,
}

/// <summary>
/// One key of a shader setting over time: the instance uniform it sets, when,
/// its value (up to a vec4; <see cref="Size"/> says how many components are
/// used), and how it eases into the next key.
/// </summary>
public readonly record struct ShaderKey(string Param, float Time, Vector4 Value, int Size, EasePreset Ease)
{
    /// <summary>Instance uniforms are at most a vec4 (render-architecture §5.3).</summary>
    public const int MaxSize = 4;
}

/// <summary>
/// The settings of one layer in the level's layer table: its name and its pass.
/// A layer without any is a plain layer and has no entry.
/// </summary>
public sealed class LevelLayer
{
    public string Name = "";
    public LayerPassKind Pass;

    /// <summary>The shader slot the pass draws with, or 0 for none.</summary>
    public int PassShader;

    /// <summary>The pass shader's keyed settings, on the layer's own track.</summary>
    public List<ShaderKey> PassKeys = new();

    /// <summary>Whether the layer has anything worth keeping in the table.</summary>
    public bool HasSettings => Name.Length > 0 || Pass != LayerPassKind.None || PassShader != 0 || PassKeys.Count > 0;

    public LevelLayer Clone() => new()
    {
        Name = Name,
        Pass = Pass,
        PassShader = PassShader,
        PassKeys = new List<ShaderKey>(PassKeys),
    };

    public bool SameAs(LevelLayer other)
    {
        if (Name != other.Name || Pass != other.Pass || PassShader != other.PassShader ||
            PassKeys.Count != other.PassKeys.Count)
        {
            return false;
        }

        for (int i = 0; i < PassKeys.Count; i++)
        {
            if (PassKeys[i] != other.PassKeys[i])
                return false;
        }

        return true;
    }
}

/// <summary>
/// The level's layer table (Docs/render-architecture.md step 5), on the level's
/// entity beside <see cref="LevelGroups"/>: an entry for each layer number that
/// has settings. Saved with the level and part of undo.
/// </summary>
public struct LevelLayers : IComponent
{
    public Dictionary<int, LevelLayer> Layers;

    public static LevelLayers Create() => new() { Layers = new Dictionary<int, LevelLayer>() };

    /// <summary>The layer table on an entity, or an empty one.</summary>
    public static LevelLayers Of(Entity level) =>
        !level.IsNull && level.TryGetComponent(out LevelLayers layers) && layers.Layers is not null ? layers : Create();

    public readonly string NameOf(int layer) =>
        Layers is not null && Layers.TryGetValue(layer, out LevelLayer? entry) ? entry.Name : "";

    /// <summary>The entry for a layer, made if it has none yet.</summary>
    public LevelLayer Edit(int layer)
    {
        Layers ??= new Dictionary<int, LevelLayer>();
        if (!Layers.TryGetValue(layer, out LevelLayer? entry))
        {
            entry = new LevelLayer();
            Layers[layer] = entry;
        }

        return entry;
    }

    /// <summary>Renames a layer; a blank name takes the entry out again if nothing else is set.</summary>
    public void SetName(int layer, string name)
    {
        LevelLayer entry = Edit(layer);
        entry.Name = name.Trim();
        Prune(layer);
    }

    /// <summary>Drops a layer's entry once it has no settings left.</summary>
    public readonly void Prune(int layer)
    {
        if (Layers is not null && Layers.TryGetValue(layer, out LevelLayer? entry) && !entry.HasSettings)
            Layers.Remove(layer);
    }

    /// <summary>Renumbers every entry; used when adding a layer pushes the others out.</summary>
    public void Renumber(Func<int, int> moved)
    {
        if (Layers is null)
            return;

        var renumbered = new Dictionary<int, LevelLayer>();
        foreach ((int layer, LevelLayer entry) in Layers)
            renumbered[moved(layer)] = entry;
        Layers = renumbered;
    }

    public readonly LevelLayers Clone()
    {
        var copy = new Dictionary<int, LevelLayer>();
        if (Layers is not null)
        {
            foreach ((int layer, LevelLayer entry) in Layers)
                copy[layer] = entry.Clone();
        }

        return new LevelLayers { Layers = copy };
    }

    public readonly bool SameAs(LevelLayers other)
    {
        Dictionary<int, LevelLayer> mine = Layers ?? new();
        Dictionary<int, LevelLayer> theirs = other.Layers ?? new();
        if (mine.Count != theirs.Count)
            return false;

        foreach ((int layer, LevelLayer entry) in mine)
        {
            if (!theirs.TryGetValue(layer, out LevelLayer? match) || !entry.SameAs(match))
                return false;
        }

        return true;
    }
}
