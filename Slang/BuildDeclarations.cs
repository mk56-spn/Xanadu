using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// What a graph keeps, read from its declarations: lists, counters and images, each a type and a name with plain
/// attributes before it.
/// <code>
/// [Size(256)] List&lt;Part&gt; parts;               // 256 parts, kept from frame to frame
/// [Cached] [Size(RING)] List&lt;Meshlet&gt; meshlets; // filled only when the values change
/// [Host] [Size(WIRES)] List&lt;Wire&gt; wires;         // the host fills it
/// [Host] List&lt;Wire&gt; wires;                      // the host sizes and fills it at run time
/// List&lt;Placed&gt; placed;                         // the host sizes it at run time, the passes fill it
/// Counter drawn;                                // one uint to count with: drawn.add(1)
/// Image&lt;float4&gt; shown;                          // the item's box, colour
/// [Clear(1e30)] Image&lt;float&gt; depth;
/// [Scale(0.5)] Image&lt;float4&gt; shadowed;          // half the box
/// [Size(256, 256)] Image&lt;float&gt; heights;       // fixed
/// [Kept] Image&lt;float4&gt; paint;                   // kept from frame to frame
/// [History(shown)] Image&lt;float4&gt; before;        // last frame's shown
/// [Shared] Image&lt;float&gt; mask;                   // one for the frame, the screen's size, whatever material declares it
/// [Shared] [Size(64)] List&lt;Hit&gt; hits;            // one list, whatever material declares it
/// Stored&lt;Book&gt; book;                            // one Book, kept, used as book.written
/// [History(deaths)] List&lt;Death&gt; lastDeaths;     // last frame's deaths: the two swap each frame
/// [View] Image&lt;float4&gt; mirrored;               // the screen's size, drawn by the host from the subtree the material names
/// </code>
/// Gathered, Slots and Meshes keep several lists under one name (<see cref="BuildBundle"/>).
/// An image's texel format comes from what it holds and its <c>[Precise]</c> or <c>[Compact]</c> (<see cref="Texel.All"/>):
/// <c>float</c> is r32f, <c>float2</c> rg16f, <c>float4</c> rgba16f, uints and ints 32 bits a channel.
/// <c>[Shared]</c> makes a list or image the frame's, not the material's or the item's: every graph that declares one of
/// that name and type gets the same, and materials that write it run before materials that read it. <c>[View]</c> makes
/// an image the host's: before the graph runs it draws the canvas subtree the material names under that name
/// (<c>CanvasMaterial.SetView</c>) into it, so it is rgba8, the screen's size and only read.
/// </summary>
internal static partial class BuildDeclarations
{
    private const string Forms =
        "[Size(n)] List<T> name; (with [Cached], [Host], [Shared] or [Reported] before it if wanted), List<T> name; (sized by the host, [Host] before it if wanted), [History(list)] List<T> name;, Stored<T> name; "
        + "(with [Cached], [Host], [Shared] or [Reported] before it if wanted), Counter name;, or Image<T> name; "
        + "(T float, float2, float4, uint, uint2, uint4, int, int2 or int4; with [Kept], [Cached], [History(image)] or [Shared], [Scale(s)] or [Size(w, h)], "
        + "[Clear(value)], and [Precise] or [Compact] before it if wanted; or [View] Image<float4> name; alone), [Size(n)] Gathered<T> name;, [Size(n)] Slots name; "
        + "or a library block (with [Cached] before it if it may be)";

    private static readonly string[] Words = ["Size", "Cached", "Host", "Kept", "History", "Scale", "Clear", "Precise", "Compact", "Shared", "View", "Reported"];

    [GeneratedRegex(@"(?<attributes>(?:\[\s*\w+\s*(?:\([^\]]*\))?\s*\]\s*)*)(?<coherent>globallycoherent\s+)?\b(?<kind>List|Image|Counter|Stored|Gathered|Slots)\b\s*(?:<\s*(?<type>[\w:.]+)\s*>)?\s*(?<name>\w+)\s*;")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\[\s*(?<word>\w+)\s*(?:\((?<arguments>[^\]]*)\))?\s*\]")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"(?<attributes>(?:\[\s*\w+\s*(?:\([^\]]*\))?\s*\]\s*)*)\b(?<type>[A-Z]\w*)\s*(?:<\s*(?<holds>[\w:.]+)\s*>)?\s+(?<name>\w+)\s*;")]
    private static partial Regex BlockUse();

    /// <summary>The library blocks <paramref name="code"/> declares at its top level (<c>[Size(640, 360)] Field field;</c>), with their types.</summary>
    private static List<(Match Match, BlockType Block)> Blocks(string code)
    {
        var found = new List<(Match, BlockType)>();
        foreach (Match match in BlockUse().Matches(code))
        {
            string type = match.Groups["type"].Value;
            if (BundleKinds.Kinds.ContainsKey(type) || type is "List" or "Image" or "Counter" or "Stored")
                continue;
            int depth = 0;
            foreach (char c in code.AsSpan(0, match.Index))
                depth += c == '{' ? 1 : c == '}' ? -1 : 0;
            if (depth == 0 && StackLibrary.Block(type) is { } block)
                found.Add((match, block));
        }

        return found;
    }

    /// <summary>A stray graph attribute's place in <paramref name="code"/>, or -1: one that sits on no declaration.</summary>
    public static int Stray(string code)
    {
        HashSet<int> held = [.. Declaration().Matches(code).Concat(Blocks(code).Select(use => use.Match))
            .SelectMany(match => Attribute().Matches(match.Groups["attributes"].Value)
            .Select(attribute => match.Groups["attributes"].Index + attribute.Index))];
        foreach (Match attribute in Attribute().Matches(code))
            if (Words.Contains(attribute.Groups["word"].Value) && !held.Contains(attribute.Index))
                return attribute.Index;
        return -1;
    }

    /// <summary>True when <paramref name="code"/> declares anything a graph keeps.</summary>
    public static bool Any(string code) => Declaration().IsMatch(code) || Stray(code) >= 0;

    /// <summary>The lists, images and bundles of <paramref name="code"/>, in order; throws <see cref="BuildProblem"/> when one is written wrong.</summary>
    public static (List<BuildBuffer> Buffers, List<BuildImage> Images, List<BuildBundle> Bundles) Read(string code,
        System.Func<string, BuildValue?> constant)
    {
        if (Stray(code) is var stray and >= 0)
            throw new BuildProblem(stray, $"this attribute goes on a declaration: {Forms}.");

        List<BuildBuffer> buffers = [];
        List<BuildImage> images = [];
        List<BuildBundle> bundles = [];
        long book = 0;
        string? keeper = null;
        List<(Match Match, Dictionary<string, DeclaredArgument?> Said)> histories = [], listHistories = [];
        foreach (Match match in Declaration().Matches(code))
        {
            string name = match.Groups["name"].Value, kind = match.Groups["kind"].Value;
            if (name == BundleKinds.Bookkeeping || buffers.Any(buffer => buffer.Name == name || buffer.Bundle == name)
                || images.Any(image => image.Name == name) || bundles.Any(bundle => bundle.Name == name))
                throw new BuildProblem(match.Index, $"{name} is declared twice.");
            Dictionary<string, DeclaredArgument?> said = Said(match, kind);
            if (kind == "Image" && said.ContainsKey("History"))
                histories.Add((match, said));
            else if (kind == "List" && said.ContainsKey("History"))
                listHistories.Add((match, said));
            else if (kind == "Image")
                images.Add(ImageDeclarations.Read(match, said, constant));
            else if (BundleKinds.Kinds.ContainsKey(kind))
            {
                bundles.Add(ReadBundle(match, said, kind, buffers, ref book, constant));
                if (book > 0)
                    keeper ??= bundles[^1].Name;
            }
            else
                buffers.Add(ReadBuffer(match, said, kind, constant));
        }

        foreach ((Match match, BlockType block) in Blocks(code))
        {
            string name = match.Groups["name"].Value;
            if (name == BundleKinds.Bookkeeping || buffers.Any(buffer => buffer.Name == name || buffer.Bundle == name)
                || images.Any(image => image.Name == name) || bundles.Any(bundle => bundle.Name == name))
                throw new BuildProblem(match.Index, $"{name} is declared twice.");
            string? holds = match.Groups["holds"] is { Success: true } held ? held.Value : null;
            if ((holds is null) != (block.Generic is null))
                throw new BuildProblem(match.Index, block.Generic is null
                    ? $"{name} holds what a {block.Name} always does: {block.Name} {name};."
                    : $"say what {name} holds: {block.Name}<T> {name};.");
            (long[] sizes, bool cached) = BlockSizes(match, block, constant);
            bundles.Add(BuildBlocks.Lay(block, name, holds, sizes, cached, match.Index, match.Index + match.Length, buffers, ref book));
            if (book > 0)
                keeper ??= name;
        }

        // After every image, so a history may come before its source.
        int sources = images.Count;
        foreach ((Match match, Dictionary<string, DeclaredArgument?> said) in histories)
            images.Add(ImageDeclarations.ReadHistory(match, said, images, sources));
        int lists = buffers.Count;
        foreach ((Match match, Dictionary<string, DeclaredArgument?> said) in listHistories)
            buffers.Add(ReadListHistory(match, said, buffers, lists));
        // The bookkeeping is declared with the first bundle that keeps anything in it.
        if (keeper is not null)
        {
            BuildBundle first = bundles.Find(bundle => bundle.Name == keeper)!;
            buffers.Add(new BuildBuffer(BundleKinds.Bookkeeping, "uint", book, false, first.Start, first.End, Bundle: keeper));
        }

        ClaimNames(code, [
            .. buffers.Where(buffer => buffer.Bundle is null).Select(buffer => (buffer.Name, buffer.Start, buffer.End)),
            .. images.Select(image => (image.Name, image.Start, image.End)),
            .. bundles.Select(bundle => (bundle.Name, bundle.Start, bundle.End))]);
        return (buffers, images, bundles);
    }

    /// <summary>Words that can stand before a name without declaring it: <c>return pool;</c> uses pool.</summary>
    private static readonly HashSet<string> NotTypes = ["return", "case", "else", "goto", "throw", "new", "in", "out", "inout", "const", "static"];

    [GeneratedRegex(@"(?<![\w.:])(?<type>[A-Za-z_]\w*(?:(?:\.|::)\w+)*(?:<[^<>;(){}]*>)?)\s+(?<name>[A-Za-z_]\w*)\s*(?=[(;=,)\[:])")]
    private static partial Regex Named();

    /// <summary>
    /// Throws when anything else in <paramref name="code"/> (a function, a global, a local, a parameter) takes the name of
    /// something the graph keeps: the graph's name would be hidden there, or clash outright, and Slang's word for it is far
    /// from the declaration.
    /// </summary>
    private static void ClaimNames(string code, List<(string Name, int Start, int End)> kept)
    {
        // A struct's fields are reached through a value (p.alive), so they never hide a name.
        List<(int Open, int Close)> structs = [];
        foreach (Match body in Regex.Matches(code, @"\bstruct\s+\w+[^{;]*\{"))
            structs.Add((body.Index, BuildText.Matching(code, body.Index + body.Length - 1)));
        Dictionary<string, (int Start, int End)> owned = [];
        foreach ((string name, int start, int end) in kept)
            owned[name] = (start, end);
        foreach (Match match in Named().Matches(code))
        {
            string name = match.Groups["name"].Value;
            if (!owned.TryGetValue(name, out var declared) || match.Index >= declared.Start && match.Index < declared.End
                || NotTypes.Contains(match.Groups["type"].Value) || structs.Exists(body => match.Index > body.Open && match.Index < body.Close))
                continue;
            bool function = code.AsSpan(match.Index + match.Length).TrimStart().StartsWith("(");
            throw new BuildProblem(match.Groups["name"].Index,
                $"{name} is already something the graph keeps; give this {(function ? "function" : "variable")} another name.");
        }
    }

    /// <summary>A declaration's attributes, by word, with their arguments; throws for one that doesn't belong on its kind.</summary>
    private static Dictionary<string, DeclaredArgument?> Said(Match match, string kind)
    {
        string[] allowed = kind switch
        {
            "List" => ["Size", "Cached", "Host", "Shared", "History", "Reported"],
            "Stored" => ["Cached", "Host", "Shared", "Reported"],
            "Counter" => [],
            "Gathered" or "Slots" => [BundleKinds.Kinds[kind].SizeWord],
            _ => ["Kept", "Cached", "History", "Scale", "Size", "Clear", "Precise", "Compact", "Shared", "View"],
        };
        Dictionary<string, DeclaredArgument?> said = [];
        Group attributes = match.Groups["attributes"];
        foreach (Match attribute in Attribute().Matches(attributes.Value))
        {
            string word = attribute.Groups["word"].Value;
            if (!allowed.Contains(word))
                throw new BuildProblem(attributes.Index + attribute.Index, kind == "Counter"
                    ? "a Counter takes no attributes: Counter name;."
                    : $"[{word}] doesn't go on a{(kind == "Image" ? "n" : "")} {kind}: {Forms}.");
            if (!said.TryAdd(word, attribute.Groups["arguments"].Success
                    ? new DeclaredArgument(attribute.Groups["arguments"].Value, attributes.Index + attribute.Groups["arguments"].Index)
                    : null))
                throw new BuildProblem(attributes.Index + attribute.Index, $"[{word}] is said twice.");
        }

        if (match.Groups["coherent"].Success && kind != "List")
            throw new BuildProblem(match.Index, "only a List can be globallycoherent.");
        bool generic = kind is "List" or "Image" or "Stored" || BundleKinds.Kinds.TryGetValue(kind, out var bundle) && bundle.Generic;
        if (!generic && match.Groups["type"].Success)
            throw new BuildProblem(match.Index, $"a {kind} holds what it always does: {kind} {match.Groups["name"].Value};.");
        if (generic && !match.Groups["type"].Success)
            throw new BuildProblem(match.Index, $"say what a{(kind == "Image" ? "n" : "")} {kind} holds: {kind}<T> {match.Groups["name"].Value};.");
        if (kind == "Image" && (said.ContainsKey("Kept") && said.ContainsKey("Cached") || said.ContainsKey("History") && (said.ContainsKey("Kept") || said.ContainsKey("Cached"))))
            throw new BuildProblem(match.Index, "an image is [Kept], [Cached] or a [History], one of them.");
        // The frame's: no material's values to cache by, no material's data to fill it from, no item to keep it for.
        if (said.ContainsKey("Shared") && (said.ContainsKey("Kept") || said.ContainsKey("Cached") || said.ContainsKey("History") || said.ContainsKey("Host")))
            throw new BuildProblem(match.Index, kind != "Image"
                ? $"a [Shared] {kind} is the frame's, kept from frame to frame like any list; it can't be [Cached], [Host] or a [History] too."
                : "a [Shared] image is the frame's, cleared each frame; it can't be [Kept], [Cached] or a [History] too.");
        // The host's: what the canvas draws, as big as the screen, so nothing about it is the graph's to say.
        if (said.ContainsKey("View") && (said.Count > 1 || match.Groups["type"].Value != "float4"))
            throw new BuildProblem(match.Index,
                $"a [View] image is the canvas drawn again, rgba8 and the screen's size, so it takes nothing else: [View] Image<float4> {match.Groups["name"].Value};.");
        if (said.ContainsKey("Precise") && said.ContainsKey("Compact"))
            throw new BuildProblem(match.Index, "an image is [Precise] or [Compact], not both.");
        return said;
    }

    /// <summary>
    /// A block declaration's sizes, <c>[Size(640, 360)]</c>, one whole number for each the block is sized by, and whether it is
    /// <c>[Cached]</c>.
    /// </summary>
    private static (long[] Sizes, bool Cached) BlockSizes(Match match, BlockType block, System.Func<string, BuildValue?> constant)
    {
        string name = match.Groups["name"].Value, form = (block.Cacheable ? "([Cached]) " : "") + (block.Sizes.Length == 0
            ? $"{block.Name} {name};"
            : $"[Size({string.Join(", ", block.Sizes)})] {block.Name} {name};");
        Group attributes = match.Groups["attributes"];
        Match[] all = [.. Attribute().Matches(attributes.Value).Cast<Match>()];
        bool cached = all.Any(attribute => attribute.Groups["word"].Value == "Cached");
        if (cached && !block.Cacheable)
            throw new BuildProblem(match.Index, $"a {block.Name} changes every frame (it keeps a history, Slots or Gathered), so it is never [Cached].");
        Match[] said = [.. all.Where(attribute => attribute.Groups["word"].Value != "Cached")];
        if (said.Any(attribute => attribute.Groups["word"].Value != "Size") || said.Length > 1 || said.Length != (block.Sizes.Length > 0 ? 1 : 0)
            || all.Length - said.Length > 1)
            throw new BuildProblem(match.Index, $"a {block.Name} is declared as {form}");
        if (block.Sizes.Length == 0)
            return ([], cached);
        Group arguments = said[0].Groups["arguments"];
        int at = attributes.Index + arguments.Index;
        string[] given = BuildText.SplitTop(arguments.Value);
        if (given.Length != block.Sizes.Length)
            throw new BuildProblem(at, $"a {block.Name} is sized by {string.Join(" and ", block.Sizes)}: {form}");
        var sizes = new long[given.Length];
        for (int k = 0; k < given.Length; k++)
        {
            BuildValue value = BuildPasses.Evaluate(BuildPasses.ParseAt(given[k], at), at, constant);
            if (!value.Whole || value.Long < 1)
                throw new BuildProblem(at, $"{name}'s {block.Sizes[k]} is a whole number, at least 1 (it has {value}).");
            sizes[k] = value.Long;
        }

        return (sizes, cached);
    }

    private static BuildBundle ReadBundle(Match match, Dictionary<string, DeclaredArgument?> said, string kind, List<BuildBuffer> buffers,
        ref long book, System.Func<string, BuildValue?> constant)
    {
        string name = match.Groups["name"].Value, type = match.Groups["type"].Value;
        string word = BundleKinds.Kinds[kind].SizeWord;
        if (!said.TryGetValue(word, out DeclaredArgument? size) || size is null)
            throw new BuildProblem(match.Index, $"say how big {name} is: [{word}(n)] {kind}{(type == "" ? "" : $"<{type}>")} {name};.");
        BuildValue value = BuildPasses.Evaluate(BuildPasses.ParseAt(size.Value, size.Index), size.Index, constant);
        if (!value.Whole || value.Long < 1)
            throw new BuildProblem(size.Index, $"a {kind}'s {word.ToLowerInvariant()} is a whole number, at least 1 ({name} has {value}).");
        return BundleKinds.Lay(kind, name, type, value.Long, match.Index, match.Index + match.Length, buffers, ref book);
    }

    private static BuildBuffer ReadBuffer(Match match, Dictionary<string, DeclaredArgument?> said, string kind, System.Func<string, BuildValue?> constant)
    {
        string name = match.Groups["name"].Value;
        long count = 1;
        if (kind == "List")
        {
            // No size: the host sizes it at run time (CanvasMaterial.SizeBuffer), so nothing that keeps a copy of it applies.
            if (!said.TryGetValue("Size", out DeclaredArgument? size) || size is null)
            {
                if (said.Keys.Any(word => word != "Host"))
                    throw new BuildProblem(match.Index,
                        $"a list with no [Size] is sized by the host at run time, so it takes nothing but [Host]: [Size(n)] List<{match.Groups["type"].Value}> {name}; or List<{match.Groups["type"].Value}> {name};.");
                return new BuildBuffer(name, match.Groups["type"].Value, 0, match.Groups["coherent"].Success, match.Index,
                    match.Index + match.Length, Host: said.ContainsKey("Host"));
            }

            BuildValue value = BuildPasses.Evaluate(BuildPasses.ParseAt(size.Value, size.Index), size.Index, constant);
            if (!value.Whole || value.Long < 1)
                throw new BuildProblem(size.Index, $"a list's size is a whole number of elements, at least 1 ({name} has {value}).");
            count = value.Long;
        }

        return new BuildBuffer(name, kind == "Counter" ? "uint" : match.Groups["type"].Value, count, match.Groups["coherent"].Success,
            match.Index, match.Index + match.Length, Host: said.ContainsKey("Host"), Cached: said.ContainsKey("Cached"), Shared: said.ContainsKey("Shared"),
            Stored: kind == "Stored", Reported: said.ContainsKey("Reported"));
    }

    /// <summary><c>[History(list)] List&lt;T&gt; name;</c>: the list's other half, as big and of the same type, swapped with it each frame.</summary>
    private static BuildBuffer ReadListHistory(Match match, Dictionary<string, DeclaredArgument?> said, List<BuildBuffer> buffers, int lists)
    {
        string name = match.Groups["name"].Value, type = match.Groups["type"].Value;
        if (said.Keys.Any(word => word != "History") || match.Groups["coherent"].Success)
            throw new BuildProblem(match.Index, $"a history is its list's other half, so it takes nothing but [History(list)]: [History(list)] List<T> {name};.");
        string from = said["History"]?.Value.Trim() ?? "";
        int source = buffers.FindIndex(0, lists, buffer => buffer.Name == from && buffer.Bundle is null);
        if (source < 0)
            throw new BuildProblem(match.Index, $"{name} is the history of {from}, which isn't a List this graph declares.");
        BuildBuffer of = buffers[source];
        if (of.Stored)
            throw new BuildProblem(match.Index, $"{from} is one stored value, not a List: only a List or Counter keeps a history.");
        if (of.HostSized)
            throw new BuildProblem(match.Index, $"{from} is sized by the host, so it keeps no history.");
        if (of.Host || of.Cached || of.Shared)
            throw new BuildProblem(match.Index, $"{from} is [{(of.Host ? "Host" : of.Cached ? "Cached" : "Shared")}], so it keeps no history.");
        if (buffers.Any(buffer => buffer.Source == source))
            throw new BuildProblem(match.Index, $"{from} has a history already.");
        if (type != of.Type)
            throw new BuildProblem(match.Index, $"{from} holds {of.Type}, so its history does too: [History({from})] List<{of.Type}> {name};.");
        return new BuildBuffer(name, type, of.Count, of.Coherent, match.Index, match.Index + match.Length, Source: source);
    }

}

/// <summary>An attribute's argument text and where it starts in the code.</summary>
internal sealed record DeclaredArgument(string Value, int Index);
