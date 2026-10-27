using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

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
    GroupEase Ease = GroupEase.Linear)
{
    public static GroupEffect Move(
        float start,
        float end,
        Vector2 offset,
        GroupEase ease = GroupEase.Linear) =>
        new(GroupEffectKind.Move, start, end, offset, Colors.White, ease);

    public static GroupEffect Colour(
        float start,
        float end,
        Color tint,
        GroupEase ease = GroupEase.Linear) =>
        new(GroupEffectKind.Color, start, end, Vector2.Zero, tint, ease);

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

/// <summary>One group slot: a name and its timed effects.</summary>
public sealed class GroupDefinition
{
    public string Name = "";
    public List<GroupEffect> Effects = new();

    public bool IsEmpty => Name.Length == 0 && Effects.Count == 0;

    public GroupDefinition Clone() => new()
    {
        Name = Name,
        Effects = new List<GroupEffect>(Effects),
    };

    public bool SameAs(GroupDefinition other) =>
        Name == other.Name && Effects.SequenceEqual(other.Effects);
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

/// <summary>Where an item sits without its groups' moves, kept while a play scene moves it.</summary>
public struct GroupBase(Vector2 position) : IComponent
{
    public Vector2 Position = position;
}

/// <summary>
/// The colour a group fades an item towards: <see cref="Mix"/> at <see cref="Coverage"/>
/// of the way from the item's own colour. Drawing blends the item's colour into it.
/// </summary>
public struct GroupColor(Color mix, float coverage) : IComponent
{
    public Color Mix = mix;
    public float Coverage = coverage;

    /// <summary>The item's colour with the groups' fade applied; its own transparency is kept.</summary>
    public readonly Color Apply(Color basis)
    {
        if (Coverage <= 0f)
            return basis;

        float alpha = basis.A * Mathf.Lerp(1f, Mix.A, Coverage);
        Color blended = new Color(basis.R, basis.G, basis.B).Lerp(new Color(Mix.R, Mix.G, Mix.B), Coverage);
        return new Color(blended.R, blended.G, blended.B, alpha);
    }

    /// <summary>The colour to draw an entity in: its own, or faded by its groups when it has any.</summary>
    public static Color For(Entity entity, Color basis) =>
        entity.TryGetComponent(out GroupColor group) ? group.Apply(basis) : basis;
}
