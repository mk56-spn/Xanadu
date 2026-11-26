using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One list a block keeps: <c>[Size(w * h)] List&lt;float&gt; cells;</c>, <c>[History(cells)] List&lt;float&gt; before;</c>,
/// <c>Stored&lt;T&gt; name;</c> or <c>Counter name;</c> in the block's struct.
/// </summary>
/// <param name="Kind">List, Stored, Counter, or a bundle the stack's bookkeeping holds: Slots or Gathered.</param>
/// <param name="Size">A list's size, over the block's sizes and the module's constants; null for one of one or a history.</param>
/// <param name="History">For a history, the part it is last frame's of; null otherwise.</param>
/// <param name="Copies">Marked <c>[Copies]</c>: its count (a List or Counter's first uint, a Gathered's count) is what a draw's
/// <c>copies: name</c> draws by.</param>
public sealed record BlockPart(string Name, string Kind, string Type, BuildExpression? Size, string? History, bool Copies = false);

/// <summary>
/// A block a library module declares: a struct that owns its lists, its passes and its functions, used by declaring it in
/// a stack (<c>[Size(640, 360)] Field field;</c>).
/// <code>
/// [Block(w, h)]
/// public struct Field
/// {
///     public uint w, h;                        // its sizes, as the declaration gives them
///     [Size(w * h)] List&lt;float&gt; cells;        // its lists
///     [History(cells)] List&lt;float&gt; before;
///     public float at(float2 uv) { … }          // its functions, called on it: field.at(uv)
/// };
/// [Pass(64, over: w * h)]                     // its passes take it first, called on it in graph(): field.spread(fade, spread)
/// public void spread(uint i, Field f, float fade, float amount) { … }
/// </code>
/// </summary>
/// <param name="Sizes">Its size names, in order: the <c>uint</c> fields the declaration's <c>[Size(…)]</c> fills.</param>
/// <param name="Fields">Every field in order, sizes and parts: what its made-up constructor takes.</param>
/// <param name="Generic">The type it holds, as its struct names it (<c>T</c> in <c>Events&lt;T&gt;</c>); null for a block of fixed types.</param>
public sealed record BlockType(string Name, StackLibrary Library, string[] Sizes, BlockPart[] Parts, string[] Fields, string? Generic = null)
{
    /// <summary>It may be declared <c>[Cached]</c>: none of its parts changes every frame on its own (no history, Slots or Gathered).</summary>
    public bool Cacheable => System.Array.TrueForAll(Parts, part => part.History is null && part.Kind is not ("Slots" or "Gathered"));
}

/// <summary>Reads blocks out of a library module, prepares them for Slang, and lays one out where a stack declares it.</summary>
internal static partial class BuildBlocks
{
    [GeneratedRegex(@"\[\s*Block\s*(?:\((?<sizes>[^\]]*)\))?\s*\]\s*(?:public\s+)?struct\s+(?<name>\w+)\s*(?:<\s*(?<generic>\w+)\s*>)?\s*\{")]
    public static partial Regex BlockDeclaration();

    [GeneratedRegex(@"^(?<attributes>(?:\[\s*\w+\s*(?:\([^\]]*\))?\s*\]\s*)*)(?:public\s+)?(?<kind>List|Stored|Counter|Slots|Gathered|uint)\b\s*(?:<\s*(?<type>[\w:.]+)\s*>)?\s*(?<names>\w+(?:\s*,\s*\w+)*)\s*$", RegexOptions.Singleline)]
    private static partial Regex Field();

    [GeneratedRegex(@"\[\s*(?<word>Size|History|Copies)\s*(?:\((?<argument>[^\]]*)\))?\s*\]")]
    private static partial Regex PartAttribute();

    /// <summary>A field statement of a block's body: its text and where it sits.</summary>
    private readonly record struct Statement(string Text, int Start, int End);

    /// <summary>The statements at the top of a struct's body that aren't functions: its fields.</summary>
    private static List<Statement> Fields(string code, int open, int close)
    {
        var fields = new List<Statement>();
        int start = open + 1;
        for (int i = open + 1; i < close; i++)
        {
            if (code[i] == '{')
            {
                // A function's body (or an initialiser's): what came before it is no field.
                i = BuildText.Matching(code, i);
                start = i + 1;
            }
            else if (code[i] == ';')
            {
                if (code[start..i].Trim() is { Length: > 0 } text)
                    fields.Add(new Statement(text, start + (code[start..i].Length - code[start..i].TrimStart().Length), i + 1));
                start = i + 1;
            }
        }

        return fields;
    }

    /// <summary>Every block <paramref name="code"/> (comments blanked) declares; throws <see cref="BuildProblem"/> for one written wrong.</summary>
    public static List<BlockType> Read(string code, StackLibrary library)
    {
        var blocks = new List<BlockType>();
        foreach (Match match in BlockDeclaration().Matches(code))
        {
            string name = match.Groups["name"].Value;
            string[] sizes = match.Groups["sizes"].Success
                ? [.. match.Groups["sizes"].Value.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries)]
                : [];
            int open = match.Index + match.Length - 1, close = BuildText.Matching(code, open);
            if (close < 0)
                throw new BuildProblem(match.Index, $"the block {name} needs a body in braces.");
            var parts = new List<BlockPart>();
            var fields = new List<string>();
            foreach (Statement statement in Fields(code, open, close))
            {
                Match field = Field().Match(statement.Text);
                if (!field.Success)
                    throw new BuildProblem(statement.Start,
                        $"a block holds its sizes (uint {string.Join(", ", sizes)}) and its lists ([Size(n)] List<T>, [History(list)] List<T>, Stored<T>, Counter), then its functions.");
                string kind = field.Groups["kind"].Value;
                string[] names = [.. field.Groups["names"].Value.Split(',', System.StringSplitOptions.TrimEntries)];
                Dictionary<string, string> said = PartAttribute().Matches(field.Groups["attributes"].Value)
                    .ToDictionary(attribute => attribute.Groups["word"].Value, attribute => attribute.Groups["argument"].Value.Trim());
                if (kind == "uint")
                {
                    if (names.Any(size => !sizes.Contains(size)) || said.Count > 0)
                        throw new BuildProblem(statement.Start, $"a block's uints are its sizes, as [Block({string.Join(", ", sizes)})] names them.");
                    fields.AddRange(names);
                    continue;
                }

                bool copies = said.Remove("Copies");
                if (copies && names.Length > 1)
                    throw new BuildProblem(statement.Start, "[Copies] marks one part: declare it on its own.");
                foreach (string part in names)
                {
                    BuildExpression? size = said.TryGetValue("Size", out string? text) ? BuildPasses.ParseAt(text, statement.Start) : null;
                    string? history = said.GetValueOrDefault("History");
                    if (kind == "List" && (size is null) == (history is null))
                        throw new BuildProblem(statement.Start, $"a block's list says its size or what it is the history of: [Size(n)] or [History(list)] List<T> {part};.");
                    if (kind is "Slots" or "Gathered" && (size is null || history is not null))
                        throw new BuildProblem(statement.Start, $"a block's {kind} says its size: [Size(n)] {kind} {part};.");
                    if (copies && !(kind is "Counter" or "Gathered" || kind == "List" && field.Groups["type"].Value == "uint" && history is null))
                        throw new BuildProblem(statement.Start, $"[Copies] goes on a block's Counter, Gathered or [Size(n)] List<uint>: what it counts is what copies: draws by.");
                    if (copies && parts.Exists(other => other.Copies))
                        throw new BuildProblem(statement.Start, $"the block {name} draws by one count: only one part is [Copies].");
                    if (kind is not ("List" or "Slots" or "Gathered") && said.Count > 0)
                        throw new BuildProblem(statement.Start, $"a {kind} in a block takes no attributes but [Copies].");
                    if (history is not null && parts.Find(of => of.Name == history) is not { Kind: "List" } source)
                        throw new BuildProblem(statement.Start, $"{part} is the history of {history}, which isn't a List the block holds before it.");
                    string type = kind == "Counter" ? "uint" : field.Groups["type"].Value;
                    parts.Add(new BlockPart(part, kind, type, size, history, copies));
                    fields.Add(part);
                }
            }

            if (sizes.FirstOrDefault(size => !fields.Contains(size)) is { } missing)
                throw new BuildProblem(match.Index, $"the block {name} is sized by {missing}, so it holds uint {missing};.");
            blocks.Add(new BlockType(name, library, sizes, [.. parts], [.. fields],
                match.Groups["generic"] is { Success: true } generic ? generic.Value : null));
        }

        return blocks;
    }

    /// <summary>
    /// The module's blocks as Slang builds them: <c>[Block]</c> and the parts' attributes blanked, a constructor taking every
    /// field in order added on the closing brace's line, and a Stored part named in the body read as its one element.
    /// </summary>
    public static string Prepared(string source, string code)
    {
        var spans = new List<(int, int, string)>();
        foreach (Match match in BlockDeclaration().Matches(code))
        {
            int open = match.Index + match.Length - 1, close = BuildText.Matching(code, open);
            if (close < 0)
                continue;
            int attributeEnd = code.IndexOf(']', match.Index) + 1;
            spans.Add((match.Index, attributeEnd, ""));
            var parameters = new List<string>();
            var stored = new List<string>();
            foreach (Statement statement in Fields(code, open, close))
            {
                Match field = Field().Match(statement.Text);
                if (!field.Success)
                    continue;
                foreach (Match attribute in PartAttribute().Matches(field.Groups["attributes"].Value))
                    spans.Add((statement.Start + attribute.Index, statement.Start + attribute.Index + attribute.Length, ""));
                string kind = field.Groups["kind"].Value;
                string type = kind switch
                {
                    "uint" => "uint",
                    "Counter" => "RWStructuredBuffer<uint>",
                    "Slots" => "Slots",
                    "Gathered" => $"Gathered<{field.Groups["type"].Value}>",
                    _ => $"RWStructuredBuffer<{field.Groups["type"].Value}>",
                };
                foreach (string name in field.Groups["names"].Value.Split(',', System.StringSplitOptions.TrimEntries))
                {
                    parameters.Add($"{type} {name}");
                    if (kind == "Stored")
                        stored.Add(name);
                }
            }

            foreach (string name in stored)
                foreach (Match use in Regex.Matches(code[..close], $@"(?<![\w.:<]){name}\b(?!\s*[;,:])"))
                    if (use.Index > open)
                        spans.Add((use.Index, use.Index + use.Length, $"{name}[0]"));
            string assigned = string.Concat(parameters.Select(parameter => parameter[(parameter.LastIndexOf(' ') + 1)..])
                .Select(name => $" this.{name} = {name};"));
            spans.Add((close, close, $"public __init({string.Join(", ", parameters)}) {{{assigned} }} "));
        }

        return spans.Count == 0 ? source : BuildText.Replaced(source, spans);
    }

    /// <summary>
    /// Block <paramref name="block"/> declared as <paramref name="name"/> with <paramref name="sizes"/> (holding
    /// <paramref name="holds"/>, for a generic one): its parts added to <paramref name="buffers"/> as <c>name_part</c>, its
    /// history parts last frame's of theirs, and the bundle that hands them to its struct.
    /// </summary>
    /// <param name="cached">Declared <c>[Cached]</c>: its lists are filled once and kept until the values they're built from move.</param>
    /// <param name="book">The uints the stack's bookkeeping holds so far: a Slots or Gathered part takes its place there.</param>
    public static BuildBundle Lay(BlockType block, string name, string? holds, long[] sizes, bool cached, int start, int end,
        List<BuildBuffer> buffers, ref long book)
    {
        (long At, string In) counted = (-1, "");
        var bundled = new Dictionary<string, string>();
        string TypeOf(BlockPart part) => part.Type == block.Generic ? holds! : part.Type;
        var given = new Dictionary<string, BuildValue>();
        for (int k = 0; k < sizes.Length; k++)
            given[block.Sizes[k]] = BuildValue.Of(sizes[k]);
        BuildValue? Lookup(string word) => given.TryGetValue(word, out BuildValue value) ? value : block.Library.Constant(word);
        foreach (BlockPart part in block.Parts)
        {
            string own = $"{name}_{part.Name}";
            if (part.History is { } of)
            {
                int source = buffers.FindIndex(buffer => buffer.Name == $"{name}_{of}");
                buffers.Add(new BuildBuffer(own, TypeOf(part), buffers[source].Count, false, start, end, Bundle: name, Source: source));
                continue;
            }

            long count = 1;
            if (part.Size is { } size)
            {
                BuildValue value = BuildPasses.Evaluate(size, start, Lookup);
                if (!value.Whole || value.Long < 1)
                    throw new BuildProblem(start, $"{name}'s {part.Name} comes to {value} elements; a list holds at least 1.");
                count = value.Long;
            }

            if (part.Kind is "Slots" or "Gathered")
            {
                // Laid out as the stack's own would be, its lists the block's: one binding for every count.
                int before = buffers.Count;
                BuildBundle inner = BundleKinds.Lay(part.Kind, own, part.Kind == "Slots" ? "" : TypeOf(part), count, start, end, buffers, ref book);
                for (int k = before; k < buffers.Count; k++)
                    buffers[k] = buffers[k] with { Bundle = name };
                bundled[part.Name] = $"{inner.Type}({inner.Made})";
                if (part.Copies)
                    counted = (inner.CountAt, inner.CountIn);
                continue;
            }

            if (part.Copies)
                counted = (0, own);
            buffers.Add(new BuildBuffer(own, TypeOf(part), count, false, start, end, Cached: cached, Bundle: name, Stored: part.Kind == "Stored"));
        }

        string made = string.Join(", ", block.Fields.Select(field =>
            System.Array.IndexOf(block.Sizes, field) is var k and >= 0 ? $"{sizes[k]}u"
            : bundled.TryGetValue(field, out string? inner) ? inner : $"{name}_{field}"));
        return new BuildBundle(name, holds is null ? block.Name : $"{block.Name}<{holds}>", start, end, made, counted.At, counted.In) { Block = block, Sizes = given };
    }
}
