using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A declaration that keeps lists under one name, as a library type made of them (<c>xanadu.store</c>) or a library block
/// (<see cref="BuildBlocks"/>):
/// <code>
/// [Size(CAP)] Gathered&lt;uint&gt; alive;   // a list threads add to, and its count
/// [Size(CAP)] Slots free;               // free places in a pool of CAP, taken and given back
/// [Cached] [Size(PAGES)] Meshes pool;   // a block: a mesh pool, its corners, triangles, pages and the pages taken
/// </code>
/// SDL_GPU binds 8 storage buffers a pass, so the small parts don't each take one: every count, every Slots and every
/// Gathered&lt;uint&gt;'s items share one uint list the build lays out (<see cref="Bookkeeping"/>), and a bundle is a place
/// in it. Other parts are lists of their own, named <c>name_part</c>. The rewrite declares them and a property by the
/// declared name that hands them to the type, so code only ever names the whole.
/// </summary>
/// <param name="Type">The library type, as the property returns it: <c>Gathered&lt;uint&gt;</c>, <c>Slots</c>, a block's name.</param>
/// <param name="Made">The type's constructor arguments: its lists by name, its places in the bookkeeping, its capacity.</param>
/// <param name="CountAt">Where its count sits in <paramref name="CountIn"/>, for a draw's <c>copies:</c>; -1 when it has none to draw by.</param>
/// <param name="CountIn">The list holding its count: the bookkeeping, or a list of its own (a block's <c>[Copies]</c> part).</param>
public sealed record BuildBundle(string Name, string Type, int Start, int End, string Made, long CountAt, string CountIn = BundleKinds.Bookkeeping)
{
    /// <summary>The library block it declares (<see cref="BlockType"/>); null for the built-in kinds.</summary>
    public BlockType? Block { get; init; }

    /// <summary>A block's sizes by name, as its declaration gave them: what its passes' <c>over:</c> reads.</summary>
    public IReadOnlyDictionary<string, BuildValue> Sizes { get; init; } = new Dictionary<string, BuildValue>();
}

/// <summary>The kinds of <see cref="BuildBundle"/>: the attribute that sizes each, and how it is laid out.</summary>
internal static class BundleKinds
{
    /// <summary>The list the bundles' counts and small parts share.</summary>
    public const string Bookkeeping = "bookkeeping";

    /// <summary>The kinds, with the attribute that sizes each and whether it holds a type of the author's.</summary>
    public static readonly IReadOnlyDictionary<string, (string SizeWord, bool Generic)> Kinds =
        new Dictionary<string, (string, bool)> { ["Gathered"] = ("Size", true), ["Slots"] = ("Size", false) };

    /// <summary>
    /// The bundle <paramref name="name"/> laid out: its own lists added to <paramref name="buffers"/>, its places taken from
    /// the bookkeeping (<paramref name="book"/>, the uints used so far).
    /// </summary>
    public static BuildBundle Lay(string kind, string name, string type, long size, int start, int end,
        List<BuildBuffer> buffers, ref long book)
    {
        BuildBuffer Own(string part, string holds, long count) =>
            new($"{name}_{part}", holds, count, false, start, end, Bundle: name);
        switch (kind)
        {
            case "Gathered" when type == "uint":
                book += 1 + size;
                return new BuildBundle(name, "Gathered<uint>", start, end, $"{Bookkeeping}, {book - size - 1}, {Bookkeeping}, {book - size}, {size}", book - size - 1);
            case "Gathered":
                buffers.Add(Own("items", type, size));
                book += 1;
                return new BuildBundle(name, $"Gathered<{type}>", start, end, $"{Bookkeeping}, {book - 1}, {name}_items, 0, {size}", book - 1);
            default:
                book += 2 + size;
                return new BuildBundle(name, "Slots", start, end, $"{Bookkeeping}, {book - size - 2}, {size}", -1);
        }
    }

    /// <summary>The bundle's own lists declared at their bindings, then the property by its name: one line, as the declaration was.</summary>
    public static string Rewritten(BuildBundle bundle, IReadOnlyList<BuildBuffer> buffers, System.Func<int, string> declared) =>
        string.Join(" ", Enumerable.Range(0, buffers.Count).Where(k => buffers[k].Bundle == bundle.Name).Select(declared))
        + $" property {bundle.Type} {bundle.Name} {{ get {{ return {bundle.Type}({bundle.Made}); }} }}";
}
