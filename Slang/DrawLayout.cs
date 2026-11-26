using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>Where one of a draw's outputs lands: its colour target and the first of its channels there.</summary>
public readonly record struct OutputSlot(int Target, int Channel);

/// <summary>
/// How a draw's outputs fill its colour targets. Up to <see cref="DrawOutput.Most"/> outputs are a target each. Past that,
/// narrow outputs share a target's channels with others of their kind (floats with floats, uints with uints, ints with
/// ints), first fit in order; four-channel outputs keep a target each. The images a call names for one shared target are
/// packed into one texture (<see cref="ImagePacking"/>).
/// </summary>
public sealed class DrawLayout
{
    private DrawLayout(OutputSlot[] slots, string[] targets)
    {
        Slots = slots;
        Targets = targets;
    }

    /// <summary>Each output's target and channel, in the outputs' order.</summary>
    public IReadOnlyList<OutputSlot> Slots { get; }

    /// <summary>What each target holds, as a Slang type (float4 for a target three channels fill).</summary>
    public IReadOnlyList<string> Targets { get; }

    /// <summary>Whether some target holds more than one output.</summary>
    public bool Packs => Targets.Count < Slots.Count;

    /// <summary>The outputs landing in target <paramref name="target"/>, by their place.</summary>
    public IEnumerable<int> In(int target) => Enumerable.Range(0, Slots.Count).Where(k => Slots[k].Target == target);

    /// <summary>
    /// The layout for <paramref name="outputs"/>; throws <see cref="BuildProblem"/> at <paramref name="at"/> when even packed
    /// they need more than <see cref="DrawOutput.Most"/> targets.
    /// </summary>
    public static DrawLayout Of(DrawOutput[] outputs, int at = 0)
    {
        if (outputs.Length <= DrawOutput.Most)
            return new DrawLayout([.. outputs.Select((_, k) => new OutputSlot(k, 0))], [.. outputs.Select(output => output.Type)]);

        var used = new List<(TexelKind Kind, int Channels)>();
        var slots = new OutputSlot[outputs.Length];
        for (int k = 0; k < outputs.Length; k++)
        {
            Texel texel = outputs[k].Texel;
            int target = texel.Channels == 4 ? -1 : used.FindIndex(held => held.Kind == texel.Kind && held.Channels + texel.Channels <= 4);
            if (target < 0)
            {
                target = used.Count;
                used.Add((texel.Kind, 0));
            }

            slots[k] = new OutputSlot(target, used[target].Channels);
            used[target] = (texel.Kind, used[target].Channels + texel.Channels);
        }

        if (used.Count > DrawOutput.Most)
            throw new BuildProblem(at, $"a draw writes {outputs.Length} outputs; packed into shared targets they still need {used.Count}, past the {DrawOutput.Most} a draw has.");
        string[] targets = [.. used.Select((held, t) => Single(outputs, slots, t) ?? Texel.TypeOf(held.Kind, held.Channels == 3 ? 4 : held.Channels))];
        return new DrawLayout(slots, targets);
    }

    /// <summary>The type of the one output alone in target <paramref name="target"/>; null when it shares it.</summary>
    private static string? Single(DrawOutput[] outputs, OutputSlot[] slots, int target)
    {
        int[] here = [.. Enumerable.Range(0, slots.Length).Where(k => slots[k].Target == target)];
        return here.Length == 1 ? outputs[here[0]].Type : null;
    }

    /// <summary>The swizzle of <paramref name="channels"/> channels from <paramref name="first"/>: <c>yz</c> for 2 from 1.</summary>
    public static string Swizzle(int first, int channels) => "xyzw".Substring(first, channels);
}
