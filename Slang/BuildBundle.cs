using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A declaration that keeps lists under one name, as a library type made of them (<c>xanadu.store</c>,
/// <c>xanadu.mesh</c>):
/// <code>
/// [Size(CAP)] Gathered&lt;uint&gt; alive;   // a list threads add to, and its count
/// [Size(CAP)] Slots free;               // free places in a pool of CAP, taken and given back
/// [Cached] [Pages(PAGES)] Meshes pool;  // a mesh pool: its corners, triangles and pages
/// </code>
/// SDL_GPU binds 8 storage buffers a pass, so the small parts don't each take one: every count, every Slots and every
/// Gathered&lt;uint&gt;'s items share one uint list the build lays out (<see cref="Bookkeeping"/>), and a bundle is a place
/// in it. Other parts are lists of their own, named <c>name_part</c>. The rewrite declares them and a property by the
/// declared name that hands them to the type, so code only ever names the whole.
/// </summary>
/// <param name="Type">The library type, as the property returns it: <c>Gathered&lt;uint&gt;</c>, <c>Slots</c>, <c>Meshes</c>.</param>
/// <param name="Made">The type's constructor arguments: its lists by name, its places in the bookkeeping, its capacity.</param>
/// <param name="CountAt">Where its count sits in the bookkeeping, for a draw's <c>copies:</c>; -1 when it has none to draw by.</param>
public sealed record BuildBundle(string Name, string Type, int Start, int End, string Made, long CountAt);

/// <summary>The kinds of <see cref="BuildBundle"/>: the attribute that sizes each, and how it is laid out.</summary>
internal static class BundleKinds
{
    /// <summary>The list the bundles' counts and small parts share.</summary>
    public const string Bookkeeping = "bookkeeping";

    /// <summary>The kinds, with the attribute that sizes each and whether it holds a type of the author's.</summary>
    public static readonly IReadOnlyDictionary<string, (string SizeWord, bool Generic)> Kinds =
        new Dictionary<string, (string, bool)> { ["Gathered"] = ("Size", true), ["Slots"] = ("Size", false), ["Meshes"] = ("Pages", false) };

    /// <summary>The words that may go before a kind's declaration: kept state that changes every frame is never cached.</summary>
    public static string[] Allowed(string kind) => kind == "Meshes" ? ["Pages", "Cached"] : [Kinds[kind].SizeWord];

    /// <summary>
    /// The bundle <paramref name="name"/> laid out: its own lists added to <paramref name="buffers"/>, its places taken from
    /// the bookkeeping (<paramref name="book"/>, the uints used so far).
    /// </summary>
    public static BuildBundle Lay(string kind, string name, string type, long size, bool cached, int start, int end,
        List<BuildBuffer> buffers, ref long book)
    {
        BuildBuffer Own(string part, string holds, long count) =>
            new($"{name}_{part}", holds, count, false, start, end, Cached: cached, Bundle: name);
        switch (kind)
        {
            case "Gathered" when type == "uint":
                book += 1 + size;
                return new BuildBundle(name, "Gathered<uint>", start, end, $"{Bookkeeping}, {book - size - 1}, {Bookkeeping}, {book - size}, {size}", book - size - 1);
            case "Gathered":
                buffers.Add(Own("items", type, size));
                book += 1;
                return new BuildBundle(name, $"Gathered<{type}>", start, end, $"{Bookkeeping}, {book - 1}, {name}_items, 0, {size}", book - 1);
            case "Slots":
                book += 2 + size;
                return new BuildBundle(name, "Slots", start, end, $"{Bookkeeping}, {book - size - 2}, {size}", -1);
            default:
                buffers.Add(Own("corners", "MeshVertex", size * 128));
                buffers.Add(Own("triangles", "uint", size * 128));
                buffers.Add(Own("pages", "Meshlet", size));
                return new BuildBundle(name, "Meshes", start, end, $"{name}_corners, {name}_triangles, {name}_pages", -1);
        }
    }

    /// <summary>The bundle's own lists declared at their bindings, then the property by its name: one line, as the declaration was.</summary>
    public static string Rewritten(BuildBundle bundle, IReadOnlyList<BuildBuffer> buffers, System.Func<int, string> declared) =>
        string.Join(" ", Enumerable.Range(0, buffers.Count).Where(k => buffers[k].Bundle == bundle.Name).Select(declared))
        + $" property {bundle.Type} {bundle.Name} {{ get {{ return {bundle.Type}({bundle.Made}); }} }}";
}
