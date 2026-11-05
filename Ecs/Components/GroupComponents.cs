using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>How many groups a level has. They are fixed slots, shown as a grid.</summary>
public static class GroupLimits
{
    public const int Count = 16;
    public const int Columns = 4;
}

/// <summary>What a group effect does to the items in the group.</summary>
public enum GroupEffectKind
{
    /// <summary>Slides the items by an offset, from nothing at the start to the full offset at the end.</summary>
    Move,

    /// <summary>Fades the items' colour to a target between the start and the end.</summary>
    Color,
}

/// <summary>
/// One timed effect of a group. It begins at <see cref="Start"/> and is done at
/// <see cref="End"/> (both in seconds); it holds its result afterwards. The end
/// is set by hand, so an effect can finish long before the next one begins.
/// </summary>
public readonly record struct GroupEffect(
    GroupEffectKind Kind,
    float Start,
    float End,
    Vector2 Offset,
    Color Tint,
    GroupEase Ease = GroupEase.Linear,
    bool Muted = false)
{
    public static GroupEffect Move(
        float start,
        float end,
        Vector2 offset,
        GroupEase ease = GroupEase.Linear,
        bool muted = false) =>
        new(GroupEffectKind.Move, start, end, offset, Colors.White, ease, muted);

    public static GroupEffect Colour(
        float start,
        float end,
        Color tint,
        GroupEase ease = GroupEase.Linear,
        bool muted = false) =>
        new(GroupEffectKind.Color, start, end, Vector2.Zero, tint, ease, muted);

    /// <summary>
    /// How far through the effect a time is, from 0 before it starts to 1 once
    /// it ends. The easing curve is applied to the fraction of the span.
    /// </summary>
    public float Progress(float seconds)
    {
        if (seconds <= Start)
            return seconds >= Start && End <= Start ? 1f : 0f;
        if (End <= Start)
            return 1f;
        return GroupEaseCurves.Apply(Ease, (seconds - Start) / (End - Start));
    }
}

/// <summary>One group slot: a name, its timed effects and its item-shader track.</summary>
public sealed class GroupDefinition
{
    public string Name = "";
    public List<GroupEffect> Effects = new();

    /// <summary>The shader slot its item-shader track draws with, or 0 for none
    /// (docs/render-architecture.md D11).</summary>
    public int Shader;

    /// <summary>The track's keyed instance-uniform values, in any order.</summary>
    public List<ShaderKey> ShaderKeys = new();

    /// <summary>Whether the group has a shader (a slot or keys on its track).</summary>
    public bool HasShader => Shader != 0 || ShaderKeys.Count > 0;

    /// <summary>Whether its members draw into one buffer first (D12). A lock: never switched off by itself.</summary>
    public bool Buffered;

    /// <summary>The buffer's tint, multiplied into the members' image.</summary>
    public Color BufferTint = Colors.White;

    /// <summary>The buffer's alpha, multiplied with the tint's.</summary>
    public float BufferAlpha = 1f;

    /// <summary>The shader slot the buffer is drawn with, or 0 for none.</summary>
    public int BufferShader;

    /// <summary>The buffer shader's keyed instance values, its own separate track (D14).</summary>
    public List<ShaderKey> BufferKeys = new();

    /// <summary>How the buffer blends into its layer; static, like an item's blend (D17).</summary>
    public Xanadu.Rendering.BlendMode BufferBlend = Xanadu.Rendering.BlendMode.Mix;

    public bool IsEmpty => Name.Length == 0 && Effects.Count == 0 && !HasShader && !Buffered;

    public GroupDefinition Clone() => new()
    {
        Name = Name,
        Effects = new List<GroupEffect>(Effects),
        Shader = Shader,
        ShaderKeys = new List<ShaderKey>(ShaderKeys),
        Buffered = Buffered,
        BufferTint = BufferTint,
        BufferAlpha = BufferAlpha,
        BufferShader = BufferShader,
        BufferKeys = new List<ShaderKey>(BufferKeys),
        BufferBlend = BufferBlend,
    };

    public bool SameAs(GroupDefinition other) =>
        Name == other.Name &&
        Effects.SequenceEqual(other.Effects) &&
        Shader == other.Shader &&
        ShaderKeys.SequenceEqual(other.ShaderKeys) &&
        Buffered == other.Buffered &&
        BufferTint == other.BufferTint &&
        BufferAlpha.Equals(other.BufferAlpha) &&
        BufferShader == other.BufferShader &&
        BufferKeys.SequenceEqual(other.BufferKeys) &&
        BufferBlend == other.BufferBlend;
}

/// <summary>The level's groups, on the editor or play entity that owns the level.</summary>
public struct LevelGroups : IComponent
{
    public GroupDefinition[] Groups;

    public static LevelGroups Create()
    {
        var groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        return new LevelGroups { Groups = groups };
    }

    public readonly LevelGroups Clone()
    {
        var copy = new GroupDefinition[Groups.Length];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = Groups[i].Clone();
        return new LevelGroups { Groups = copy };
    }

    public readonly bool SameAs(LevelGroups other)
    {
        if (Groups.Length != other.Groups.Length)
            return false;
        for (int i = 0; i < Groups.Length; i++)
        {
            if (!Groups[i].SameAs(other.Groups[i]))
                return false;
        }

        return true;
    }
}

/// <summary>
/// The groups an item belongs to, one bit per group. An item can be in any
/// number of groups; its effects from all of them apply together.
/// </summary>
public struct ItemGroups(uint mask) : IComponent
{
    public uint Mask = mask;

    public readonly bool Has(int group) => group is >= 0 and < GroupLimits.Count && (Mask & (1u << group)) != 0;

    public static uint Bit(int group) => 1u << group;

    /// <summary>How many groups a mask selects, for the badge drawn on an item.</summary>
    public static int Count(uint mask) => System.Numerics.BitOperations.PopCount(mask);

    public static List<int> ToList(uint mask)
    {
        var list = new List<int>();
        for (int i = 0; i < GroupLimits.Count; i++)
        {
            if ((mask & (1u << i)) != 0)
                list.Add(i);
        }

        return list;
    }

    public static uint FromList(IEnumerable<int>? groups)
    {
        uint mask = 0;
        if (groups is null)
            return mask;
        foreach (int group in groups)
        {
            if (group is >= 0 and < GroupLimits.Count)
                mask |= 1u << group;
        }

        return mask;
    }
}

/// <summary>
/// How far the item's groups have moved it at the playhead. It is applied when
/// the item is drawn, hit or picked, and never written into the item's own
/// position, so the placed position stays what the level, undo and saving hold
/// (docs/render-architecture.md step 3a). Worked out each frame by
/// <c>GroupModulationSystem</c>; an item no group moves has none.
/// </summary>
public struct GroupOffset(Vector2 offset) : IComponent
{
    public Vector2 Offset = offset;

    /// <summary>The item's offset, or zero.</summary>
    public static Vector2 Of(Entity entity) =>
        !entity.IsNull && entity.TryGetComponent(out GroupOffset moved) ? moved.Offset : Vector2.Zero;

    /// <summary>Where a placed playfield point of the item is shown at the playhead.</summary>
    public static Vector2 Shown(Entity entity, Vector2 placed) => placed + Of(entity);

    /// <summary>A view that draws the item's placed geometry where it is shown.</summary>
    public static PlayfieldView View(Entity entity, PlayfieldView view)
    {
        Vector2 offset = Of(entity);
        if (offset == Vector2.Zero)
            return view;
        view.ScreenOrigin += offset * view.Scale;
        return view;
    }
}

/// <summary>
/// The colour a group fades an item towards: <see cref="Mix"/> at <see cref="Coverage"/>
/// of the way from the item's own colour. Drawing blends the item's colour into it.
/// <see cref="Alpha"/> is the separate, multiplied product of every group's own
/// alpha (docs/render-architecture.md D6), so a colour in one group never brings
/// back an item another group has faded out.
/// </summary>
public struct GroupColor(Color mix, float coverage, float alpha = 1f) : IComponent
{
    public Color Mix = mix;
    public float Coverage = coverage;
    public float Alpha = alpha;

    /// <summary>The item's colour with the groups' fades applied; its own transparency is kept.</summary>
    public readonly Color Apply(Color basis)
    {
        if (Coverage <= 0f && Alpha >= 1f)
            return basis;

        Color blended = Coverage > 0f
            ? new Color(basis.R, basis.G, basis.B).Lerp(new Color(Mix.R, Mix.G, Mix.B), Coverage)
            : basis;
        return new Color(blended.R, blended.G, blended.B, basis.A * Alpha);
    }

    /// <summary>
    /// The colour to draw an entity in: its own, multiplied by its self modulate,
    /// then faded by its groups when it has any, so a finished fade overrides both.
    /// </summary>
    public static Color For(Entity entity, Color basis)
    {
        Color own = SelfModulate.For(entity, basis);
        return entity.TryGetComponent(out GroupColor group) ? group.Apply(own) : own;
    }
}

/// <summary>
/// The fade reaching a block's outline, kept apart from <see cref="GroupColor"/>,
/// which reaches its interior, so each can follow different groups.
/// </summary>
public struct GroupOutlineColor(Color mix, float coverage, float alpha = 1f) : IComponent
{
    public Color Mix = mix;
    public float Coverage = coverage;
    public float Alpha = alpha;

    /// <summary>The colour to draw a block's outline in.</summary>
    public static Color For(Entity entity, Block block) =>
        entity.TryGetComponent(out GroupOutlineColor group)
            ? new GroupColor(group.Mix, group.Coverage, group.Alpha).Apply(block.OutlineColor)
            : block.OutlineColor;

    /// <summary>The colour to draw a spike's outline in: it follows the same groups as its interior.</summary>
    public static Color For(Entity entity, Spike spike) =>
        entity.TryGetComponent(out GroupColor group) ? group.Apply(spike.OutlineColor) : spike.OutlineColor;
}
