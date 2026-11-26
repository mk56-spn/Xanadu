using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A shape's switches: the <c>bool</c>s of a step tagged as flags (<c>mirrored(x, y)</c>), read
/// from the chain and flipped by writing the step again. A step not written yet is added after the
/// shape's own placement (so <c>Circle(20).moved(…).mirrored()</c> mirrors the moved circle); its
/// arguments are written only as far as they differ from the defaults; with every flag off it is taken out.
/// </summary>
public static class SlangShapeSwitches
{
    /// <summary>Whether <paramref name="flag"/> of <paramref name="method"/> is on for the chain: off when the step isn't written.</summary>
    public static bool IsOn(ShapeChain chain, string method, IReadOnlyList<HandleTag> flags, int flag) =>
        Step(chain, method) is { } step && Values(step, flags)[flag];

    /// <summary>
    /// The edit that flips <paramref name="flag"/> (an index into <paramref name="flags"/>, the method's
    /// flags in parameter order); null when the step's switches aren't plain <c>true</c> / <c>false</c>.
    /// </summary>
    public static HandleEdit? Flip(ShapeChain chain, string method, IReadOnlyList<HandleTag> flags, int flag)
    {
        ChainStep? step = Step(chain, method);
        if (step is not null && step.Arguments.Any(argument => argument.Text is not ("true" or "false")))
            return null;
        bool[] values = step is null ? [.. flags.Select(_ => false)] : Values(step, flags);
        values[flag] = !values[flag];

        if (values.All(on => !on))
            return step is null ? null : new HandleEdit(step.Start, step.Close + 1, "", null, []);
        int written = values.Length;
        while (written > 0 && values[written - 1] == flags[written - 1].On)
            written--;
        string text = $".{method}({string.Join(", ", values.Take(written).Select(on => on ? "true" : "false"))})";
        if (step is not null)
            return new HandleEdit(step.Start, step.Close + 1, text, null, []);
        int after = ShapePlacement.FirstRunEnd(chain);
        int at = after == 0 ? chain.Close + 1 : chain.Steps[after - 1].Close + 1;
        return new HandleEdit(at, at, text, null, []);
    }

    private static ChainStep? Step(ShapeChain chain, string method) => chain.Steps.Find(step => step.Name == method);

    /// <summary>The step's flags as written, the defaults for those left out.</summary>
    private static bool[] Values(ChainStep step, IReadOnlyList<HandleTag> flags) =>
        [.. flags.Select((tag, i) => i < step.Arguments.Count ? step.Arguments[i].Text == "true" : tag.On)];
}
