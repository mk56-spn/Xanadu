using System.Collections.Generic;

namespace Xanadu.Ecs;

/// <summary>
/// What a set of groups does to an item at a moment: where it is moved to and
/// what colour it fades towards. Pure, so play and tests share it.
/// </summary>
public static class GroupModulation
{
    /// <summary>
    /// The buffered group an item with this mask draws into, or -1: only an
    /// item in exactly one group, and that group buffered (D12, D16).
    /// </summary>
    public static int BufferedGroup(IReadOnlyList<GroupDefinition> groups, uint mask)
    {
        if (mask == 0 || (mask & (mask - 1)) != 0)
            return -1;

        int group = System.Numerics.BitOperations.TrailingZeroCount(mask);
        return group < groups.Count && group < GroupLimits.Count && groups[group].Buffered ? group : -1;
    }

    /// <summary>
    /// What a buffered group's colour effects multiply its buffer's alpha by at
    /// a moment: the group's own alpha (D6), taken off its members and put on
    /// the composite so the group fades as one image (decided 2026-11-06).
    /// </summary>
    public static float BufferAlpha(
        IReadOnlyList<GroupDefinition> groups,
        int group,
        float seconds,
        Xanadu.Levels.TempoMap? tempo = null)
    {
        if (group < 0 || group >= groups.Count)
            return 1f;
        TryColor(groups, 1u << group, seconds, out _, out _, out float alpha, tempo);
        return alpha;
    }

    /// <summary>
    /// The sum of every move in every group the item is in. A move contributes
    /// its offset in proportion to how far through it the time is, and keeps
    /// the whole offset once it has ended.
    /// </summary>
    public static Vector2 Offset(
        IReadOnlyList<GroupDefinition> groups,
        uint mask,
        float seconds,
        Xanadu.Levels.TempoMap? tempo = null)
    {
        Vector2 total = Vector2.Zero;
        foreach (GroupDefinition group in Members(groups, mask))
        {
            foreach (GroupEffect effect in group.Effects)
            {
                if (effect.Kind == GroupEffectKind.Move && !effect.Muted)
                    total += effect.Offset * effect.Amount(seconds, tempo);
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
    /// <para>
    /// <paramref name="alpha"/> is what the groups multiply the item's own alpha
    /// by (Docs/render-architecture.md D6). Each group works out its own alpha
    /// from its own colour effects, the latest to start winning inside the
    /// group, so a colour in one group can never bring back an item another
    /// group has faded out; the groups' alphas then multiply.
    /// </para>
    /// </summary>
    public static bool TryColor(
        IReadOnlyList<GroupDefinition> groups,
        uint mask,
        float seconds,
        out Color mix,
        out float coverage,
        out float alpha,
        Xanadu.Levels.TempoMap? tempo = null)
    {
        var started = new List<(GroupEffect Effect, int Group)>();
        for (int i = 0; i < groups.Count && i < GroupLimits.Count; i++)
        {
            if ((mask & (1u << i)) == 0)
                continue;

            foreach (GroupEffect effect in groups[i].Effects)
            {
                if (effect.Kind == GroupEffectKind.Color && !effect.Muted && seconds >= effect.Start)
                    started.Add((effect, i));
            }
        }

        mix = Colors.White;
        coverage = 0f;
        alpha = 1f;
        if (started.Count == 0)
            return false;

        // A stable sort, so effects that start together keep their group order.
        started = Sorted(started, seconds, tempo);
        foreach ((GroupEffect effect, int _) in started)
            Crossfade(ref mix, ref coverage, effect, seconds, tempo);

        // Each group's alpha from its own effects only, then the groups' alphas
        // multiply, so a colour in one group can never undo a fade in another.
        var groupMix = new Color[GroupLimits.Count];
        var groupCoverage = new float[GroupLimits.Count];
        for (int i = 0; i < groupMix.Length; i++)
            groupMix[i] = Colors.White;

        foreach ((GroupEffect effect, int group) in started)
            Crossfade(ref groupMix[group], ref groupCoverage[group], effect, seconds, tempo);

        for (int i = 0; i < groups.Count && i < GroupLimits.Count; i++)
        {
            if ((mask & (1u << i)) == 0 || groupCoverage[i] <= 0f)
                continue;
            alpha *= Mathf.Lerp(1f, groupMix[i].A, groupCoverage[i]);
        }

        return true;
    }

    /// <summary>Fades one effect's tint over what the earlier ones left.</summary>
    private static void Crossfade(
        ref Color mix,
        ref float coverage,
        GroupEffect effect,
        float seconds,
        Xanadu.Levels.TempoMap? tempo)
    {
        float p = effect.Progress(seconds, tempo);
        float next = coverage + p - coverage * p;
        if (next <= 0f)
            return;

        // Blending the new colour over what is there, in premultiplied form so partial fades compose.
        Vector4 old = new Vector4(mix.R, mix.G, mix.B, mix.A) * (coverage * (1f - p));
        Vector4 add = new Vector4(effect.Tint.R, effect.Tint.G, effect.Tint.B, effect.Tint.A) * p;
        Vector4 blended = (old + add) / next;
        mix = new Color(blended.X, blended.Y, blended.Z, blended.W);
        coverage = next;
    }

    /// <summary>In the order their current cycles began (their starts, without cycles), so the latest wins.</summary>
    private static List<(GroupEffect Effect, int Group)> Sorted(
        List<(GroupEffect Effect, int Group)> effects,
        float seconds,
        Xanadu.Levels.TempoMap? tempo)
    {
        var indexed = new List<(GroupEffect Effect, int Group, int Order)>();
        for (int i = 0; i < effects.Count; i++)
            indexed.Add((effects[i].Effect, effects[i].Group, i));
        indexed.Sort((a, b) =>
        {
            int start = a.Effect.CycleStart(seconds, tempo).CompareTo(b.Effect.CycleStart(seconds, tempo));
            return start != 0 ? start : a.Order.CompareTo(b.Order);
        });
        return indexed.ConvertAll(entry => (entry.Effect, entry.Group));
    }

    /// <summary>
    /// The shader an item draws with when one of its groups supplies it
    /// (Docs/render-architecture.md D11, step 8): the source group's slot and
    /// its keyed values at the moment. False when the item follows its own
    /// shader instead: no group chosen, a group not in its mask, or a group
    /// whose track has no shader, which is the fallback to own.
    /// </summary>
    public static bool TryItemShader(
        IReadOnlyList<GroupDefinition> groups,
        uint mask,
        int source,
        float seconds,
        out int slot,
        out List<(string Param, Vector4 Value, int Size)> values)
    {
        slot = 0;
        values = [];
        if (source < 0 || source >= groups.Count || source >= GroupLimits.Count)
            return false;
        if ((mask & (1u << source)) == 0)
            return false;

        GroupDefinition group = groups[source];
        if (group.Shader == 0)
            return false;

        slot = group.Shader;
        values = ShaderKeyTrack.Evaluate(group.ShaderKeys, seconds);
        return true;
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
