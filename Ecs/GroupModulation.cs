using System.Collections.Generic;
using Godot;

namespace Writer.Ecs;

/// <summary>
/// What a set of groups does to an item at a moment: where it is moved to and
/// what colour it fades towards. Pure, so play and tests share it.
/// </summary>
public static class GroupModulation
{
    /// <summary>
    /// The sum of every move in every group the item is in. A move contributes
    /// its offset in proportion to how far through it the time is, and keeps
    /// the whole offset once it has ended.
    /// </summary>
    public static Vector2 Offset(IReadOnlyList<GroupDefinition> groups, uint mask, float seconds)
    {
        Vector2 total = Vector2.Zero;
        foreach (GroupDefinition group in Members(groups, mask))
        {
            foreach (GroupEffect effect in group.Effects)
            {
                if (effect.Kind == GroupEffectKind.Move)
                    total += effect.Offset * effect.Progress(seconds);
            }
        }

        return total;
    }

    /// <summary>
    /// The colour the item's groups fade it to. Colour effects apply in the
    /// order they start, each fading from what the earlier ones left, so the
    /// latest one to start wins once it is done. Returns false when no colour
    /// effect has started. <paramref name="coverage"/> is how much of the item's own
    /// colour has been replaced, 0 to 1.
    /// </summary>
    public static bool TryColor(
        IReadOnlyList<GroupDefinition> groups,
        uint mask,
        float seconds,
        out Color mix,
        out float coverage)
    {
        var started = new List<GroupEffect>();
        foreach (GroupDefinition group in Members(groups, mask))
        {
            foreach (GroupEffect effect in group.Effects)
            {
                if (effect.Kind == GroupEffectKind.Color && seconds >= effect.Start)
                    started.Add(effect);
            }
        }

        mix = Colors.White;
        coverage = 0f;
        if (started.Count == 0)
            return false;

        // A stable sort, so effects that start together keep their group order.
        started = Sorted(started);
        foreach (GroupEffect effect in started)
        {
            float p = effect.Progress(seconds);
            float next = coverage + p - coverage * p;
            if (next <= 0f)
                continue;

            // Blending the new colour over what is there, in premultiplied form so partial fades compose.
            Vector4 old = new Vector4(mix.R, mix.G, mix.B, mix.A) * (coverage * (1f - p));
            Vector4 add = new Vector4(effect.Tint.R, effect.Tint.G, effect.Tint.B, effect.Tint.A) * p;
            Vector4 blended = (old + add) / next;
            mix = new Color(blended.X, blended.Y, blended.Z, blended.W);
            coverage = next;
        }

        return coverage > 0f;
    }

    private static List<GroupEffect> Sorted(List<GroupEffect> effects)
    {
        var indexed = new List<(GroupEffect Effect, int Order)>();
        for (int i = 0; i < effects.Count; i++)
            indexed.Add((effects[i], i));
        indexed.Sort((a, b) =>
        {
            int start = a.Effect.Start.CompareTo(b.Effect.Start);
            return start != 0 ? start : a.Order.CompareTo(b.Order);
        });
        return indexed.ConvertAll(entry => entry.Effect);
    }

    private static IEnumerable<GroupDefinition> Members(IReadOnlyList<GroupDefinition> groups, uint mask)
    {
        for (int i = 0; i < groups.Count && i < GroupLimits.Count; i++)
        {
            if ((mask & (1u << i)) != 0)
                yield return groups[i];
        }
    }
}
