using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>What a level may carry in the way of custom shaders.</summary>
public static class ShaderLimits
{
    public const int Count = 10;

    /// <summary>Source longer than this is refused; real canvas shaders are a few kilobytes.</summary>
    public const int MaxSourceChars = 65_536;
}

/// <summary>One of a level's shader slots: its source and the uniform values tuned for it.</summary>
public sealed class ShaderSlot
{
    public string Name = "";
    public string Source = "";
    public Dictionary<string, float[]> Params = new();

    /// <summary>The compiled shader, built when first drawn and rebuilt if the source changes.</summary>
    public ShaderMaterial? Material;
    public string? CompiledSource;

    public ShaderSlot Clone() => new()
    {
        Name = Name,
        Source = Source,
        Params = new Dictionary<string, float[]>(Params),
    };
}

/// <summary>
/// The level's custom shaders, on the editor or play entity that owns the
/// level. Slots are numbered from 1; 0 means no shader.
/// </summary>
public struct LevelShaders : IComponent
{
    public ShaderSlot?[] Slots;

    /// <summary>The slot the player is drawn with, or 0.</summary>
    public int PlayerSlot;

    /// <summary>The slot the playfield background is drawn with, or 0.</summary>
    public int FieldSlot;

    public static LevelShaders Create() => new() { Slots = new ShaderSlot?[ShaderLimits.Count] };

    /// <summary>The shader in a 1-based slot, or null if the slot is empty or out of range.</summary>
    public readonly ShaderSlot? Get(int slot) =>
        slot is >= 1 and <= ShaderLimits.Count ? Slots[slot - 1] : null;

    public static bool IsSlot(int slot) => slot is >= 1 and <= ShaderLimits.Count;
}

/// <summary>The shader slot an item (a note, mine or block) is drawn with.</summary>
public struct ItemShader(int slot) : IComponent
{
    public int Slot = slot;

    /// <summary>The slot an entity is drawn with: 0 when it has none.</summary>
    public static int Of(Entity entity) =>
        entity.TryGetComponent(out ItemShader shader) ? shader.Slot : 0;
}
