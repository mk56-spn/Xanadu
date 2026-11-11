using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xanadu.Rendering;

namespace Xanadu.Labs.Editor;

internal enum CompletionKind
{
    Field,
    Method,
    Function,
    Constant,
    Type,
    EnumValue,
    Keyword,
    Namespace,
    Variable,
    /// <summary>A whole function the host calls (shade, update), written out with its body.</summary>
    Snippet,
}

/// <summary>
/// One thing completion offers: its name, what it is, a short signature or type, a colour for colour
/// constants, and what its <c>///</c> comment says (paragraphs and code lines split by '\n'), for hovering.
/// </summary>
internal readonly record struct Completion(string Name, CompletionKind Kind, string Detail, Vector4? Swatch = null, string? Doc = null, string? Text = null)
{
    /// <summary>Where a snippet's <see cref="Text"/> puts the caret.</summary>
    public const char Caret = '$';

    /// <summary>How hovering names it: a call with its signature, a value with its type first.</summary>
    public string Header => Kind switch
    {
        CompletionKind.Function or CompletionKind.Method => Name + Detail,
        CompletionKind.Type when Detail.StartsWith('(') => Name + Detail,
        CompletionKind.Type => Detail + " " + Name,
        CompletionKind.EnumValue => Detail + "." + Name,
        CompletionKind.Namespace => "namespace " + Name,
        CompletionKind.Snippet => Detail,
        _ => Detail + " " + Name,
    };
}

/// <summary>
/// Primitive completion from the shader library's own source. After a dot:
/// a namespace's members (<c>colours.</c>), a struct's fields, properties and
/// methods (<c>i.canvas.</c>), the shape methods every <c>IShape</c> gets, and
/// enum values (<c>blend.Mode.</c>). What a name before the dot is comes from
/// a light scan of the code being edited: <c>ItemInput i</c>,
/// <c>Canvas c = …</c>, <c>let b = Box(…)</c>. No calls in the chain, no
/// overloads, no inference beyond that.
/// <para>
/// While typing a word: the code's own names (uniforms, locals, functions),
/// what the host provides, Slang's types, keywords and common intrinsics, and
/// the library's top-level names (shapes, <c>colour</c>, <c>rotate</c>, the
/// namespaces). Not where a new name is being given (<c>float4 |</c>,
/// <c>let |</c>), and not once the word typed is already one of them.
/// </para>
/// </summary>
internal sealed partial class SlangCompletions
{
    /// <summary>Members by scope: a namespace, a struct, an interface (its extension methods) or a nested enum ("blend.Mode").</summary>
    private readonly Dictionary<string, List<Completion>> _scopes = new(StringComparer.Ordinal);

    /// <summary>Field and property types by struct, so a chain can walk on (ItemInput.canvas → Canvas).</summary>
    private readonly Dictionary<string, Dictionary<string, string>> _memberTypes = new(StringComparer.Ordinal);

    /// <summary>The library's namespaces by full name ("blend", "blend.colours"): a chain can start at one.</summary>
    private readonly HashSet<string> _namespaces = new(StringComparer.Ordinal);

    /// <summary>Every declaration by scope ("" for top level) and name, overloads included, for hovering.</summary>
    private readonly Dictionary<(string Scope, string Name), List<Completion>> _declared = new();

    /// <summary>The interfaces each struct says it implements.</summary>
    private readonly Dictionary<string, List<string>> _interfaces = new(StringComparer.Ordinal);

    [GeneratedRegex(@"\b(namespace|struct|interface|enum)\s+(\w+)\s*(?::\s*([\w\s,<>]+?))?\s*\{")]
    private static partial Regex Opener();

    [GeneratedRegex(@"\bextension\s*<\s*\w+\s*:\s*(\w+)\s*>\s*\w+\s*\{")]
    private static partial Regex ShapeExtension();

    /// <summary>An extension of one concrete type (<c>extension float4 {</c>): its members are that type's.</summary>
    [GeneratedRegex(@"\bextension\s+(\w+)\s*\{")]
    private static partial Regex TypeExtension();

    [GeneratedRegex(@"public\s+static\s+const\s+(\w+)\s+(\w+)\s*=")]
    private static partial Regex Constant();

    [GeneratedRegex(@"public\s+property\s+(\w+)\s+(\w+)")]
    private static partial Regex Property();

    [GeneratedRegex(@"public\s+(?!static\b|property\b|struct\b|enum\b|namespace\b|interface\b|extension\b)([\w<>]+)\s+(\w+)\s*;")]
    private static partial Regex Field();

    [GeneratedRegex(@"public\s+(?!static\b|property\b)([\w<>, ]+?)\s+(\w+)\s*(?:<[^>()]*>)?\s*\(([^)]*)\)")]
    private static partial Regex Method();

    [GeneratedRegex(@"([A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*)\s*\.\s*(\w*)$")]
    private static partial Regex DotChain();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+(\w+)\s*(?:=|;|,|\))")]
    private static partial Regex TypedName();

    [GeneratedRegex(@"\blet\s+(\w+)\s*=\s*([A-Za-z_]\w*)\s*\(")]
    private static partial Regex LetConstructed();

    [GeneratedRegex(@"(?<![\w.])([A-Za-z_]\w*)$")]
    private static partial Regex WordAtEnd();

    [GeneratedRegex(@"\.\s*\w*$")]
    private static partial Regex AfterDot();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+$")]
    private static partial Regex WordBeforeSpace();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*([=;,)(])")]
    private static partial Regex Declared();

    [GeneratedRegex(@"\b(?:let|var)\s+([A-Za-z_]\w*)")]
    private static partial Regex LetName();

    [GeneratedRegex(@"public\s+typealias\s+(\w+)\s*=\s*(\w+)")]
    private static partial Regex TypeAlias();

    [GeneratedRegex(@"public\s+namespace\s+(\w+)")]
    private static partial Regex NamespaceName();

    [GeneratedRegex(@"public\s+struct\s+(\w+)\s*(<?)[^{]*\{")]
    private static partial Regex StructName();

    /// <summary>A line so far that may be starting a function: an optional return type, then the name being typed.</summary>
    [GeneratedRegex(@"^(\s*)(?:(\w+)\s+)?([A-Za-z_]\w*)$")]
    private static partial Regex EntryStart();

    /// <summary>
    /// The functions the host calls, offered whole at the top level until the code has them: the
    /// return type each is declared with, its name, and the text written in ($ is where the caret goes).
    /// </summary>
    private static readonly (string Returns, string Name, string Header, string Text, string Doc)[] Entries =
    [
        ("float4", "shade", "float4 shade(Canvas canvas)",
            "float4 shade(Canvas canvas)\n{\n    $\n}",
            "The effect's pixel colour: runs once per pixel of the quad. canvas is the quad (canvas.centred, canvas.size, …); add ItemInput i for uv, the item's colour and the rest."),
        ("void", "update", "void update(inout State s, float dt)",
            "void update(inout State s, float dt)\n{\n    $\n}",
            "Runs once a frame before the item draws, changing the State every pixel then reads as state. dt is the frame's length in seconds."),
    ];

    /// <summary>update with the State it changes, for code that has no State yet.</summary>
    private const string UpdateWithState = "struct State\n{\n    $\n};\n\nvoid update(inout State s, float dt)\n{\n    \n}";

    /// <summary>The library's top-level names that are host plumbing, not for effects.</summary>
    private static readonly HashSet<string> Hidden = ["VERSION", "canvasOf", "input", "shading"];

    /// <summary>Keywords worth offering in an effect body.</summary>
    private static readonly string[] Keywords =
    [
        "if", "else", "for", "while", "return", "break", "continue", "discard", "let", "var", "const",
        "static", "uniform", "in", "out", "inout", "struct", "import", "true", "false",
    ];

    /// <summary>Slang's common intrinsics, with their usual parameters and what they do. Vectors work per component unless said.</summary>
    private static readonly (string Name, string Detail, string Doc)[] Intrinsics =
    [
        ("abs", "(x)", "x without its sign."),
        ("acos", "(x)", "The angle in radians whose cosine is x (x in -1..1); 0 to π."),
        ("asin", "(x)", "The angle in radians whose sine is x (x in -1..1); -π/2 to π/2."),
        ("atan", "(x)", "The angle in radians whose tangent is x; -π/2 to π/2. For a direction's angle use atan2."),
        ("atan2", "(y, x)", "The angle of the direction (x, y) in radians, -π to π, measured from +x. Note y comes first."),
        ("ceil", "(x)", "The smallest whole number not below x."),
        ("clamp", "(x, min, max)", "x held between min and max."),
        ("cos", "(x)", "Cosine of x radians."),
        ("cross", "(a, b)", "The cross product of two float3s: a vector at right angles to both."),
        ("ddx", "(x)", "How much x changes to the next pixel across. Neighbouring pixels share it (2×2 blocks)."),
        ("ddy", "(x)", "How much x changes to the next pixel down. Neighbouring pixels share it (2×2 blocks)."),
        ("degrees", "(radians)", "Radians turned into degrees."),
        ("distance", "(a, b)", "How far apart two points are: length(a - b)."),
        ("dot", "(a, b)", "The dot product: the sum of a × b component by component. For unit vectors, the cosine of the angle between them."),
        ("exp", "(x)", "e to the power x."),
        ("exp2", "(x)", "2 to the power x."),
        ("floor", "(x)", "The largest whole number not above x."),
        ("fmod", "(x, y)", "The remainder of x / y, with x's sign (so negative x gives a negative result). The library's mod() wraps instead."),
        ("frac", "(x)", "The part of x after the point: x - floor(x), always 0 to 1."),
        ("fwidth", "(x)", "abs(ddx(x)) + abs(ddy(x)): how much x changes over one pixel. What edges are smoothed over."),
        ("length", "(v)", "A vector's length."),
        ("lerp", "(a, b, t)", "From a to b by t: a at 0, b at 1, a + (b - a) × t. Not held to 0..1."),
        ("log", "(x)", "The natural logarithm of x."),
        ("log2", "(x)", "The base 2 logarithm of x."),
        ("max", "(a, b)", "The larger of a and b."),
        ("min", "(a, b)", "The smaller of a and b."),
        ("mul", "(a, b)", "Matrix product: a matrix times a vector, a vector times a matrix, or two matrices."),
        ("normalize", "(v)", "v scaled to length 1, pointing the same way. Undefined for a zero vector."),
        ("pow", "(x, y)", "x to the power y. Undefined for negative x."),
        ("radians", "(degrees)", "Degrees turned into radians."),
        ("reflect", "(i, n)", "Direction i bounced off a surface with unit normal n."),
        ("round", "(x)", "x to the nearest whole number."),
        ("rsqrt", "(x)", "1 / sqrt(x)."),
        ("saturate", "(x)", "x held to 0..1."),
        ("sign", "(x)", "-1, 0 or 1 by x's sign."),
        ("sin", "(x)", "Sine of x radians."),
        ("smoothstep", "(edge0, edge1, x)", "0 at edge0 to 1 at edge1, eased at both ends; held outside. Swap the edges to go 1 to 0."),
        ("sqrt", "(x)", "The square root of x."),
        ("step", "(edge, x)", "0 where x is below edge, 1 from edge on. A hard edge; smoothstep gives a soft one."),
        ("tan", "(x)", "Tangent of x radians."),
        ("tanh", "(x)", "Hyperbolic tangent: an S-curve from -1 to 1, through 0 at 0."),
        ("trunc", "(x)", "x with the part after the point dropped (towards 0)."),
    ];

    /// <summary>The library's top-level names: free functions and constants, structs, aliases, namespaces.</summary>
    private readonly List<Completion> _globals = new();

    /// <summary>Names the host declares for the code (the lab's time, mouse, …).</summary>
    private readonly List<Completion> _provided = new();

    /// <summary>Every type name a declaration can start with: Slang's, the library's structs and aliases.</summary>
    private readonly HashSet<string> _typeNames = new(SlangSyntax.BuiltInTypes, StringComparer.Ordinal);

    /// <summary>The library's type aliases and what they stand for (colour → float4), so an alias finds its type's members.</summary>
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);

    /// <param name="provided">Names the host declares for the code, with their types.</param>
    public static SlangCompletions FromSources(
        IEnumerable<string> sources, ShaderConstants constants, IEnumerable<(string Name, string Type, string? Doc)>? provided = null)
    {
        var completions = new SlangCompletions();
        foreach (string source in sources)
            completions.Read(source, constants);
        foreach ((string name, string type, string? doc) in provided ?? [])
            completions._provided.Add(new Completion(name, CompletionKind.Variable, type + " · provided", Doc: doc));
        foreach (List<Completion> members in completions._scopes.Values)
            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return completions;
    }

    /// <summary>
    /// What may follow the dot just before the caret, or complete the word
    /// being typed, filtered by what is typed; null when there is nothing to
    /// offer. <paramref name="start"/> is the column where the typed part begins.
    /// </summary>
    public List<Completion>? At(string beforeCaret, IReadOnlyList<string> code, out int start, int caretLine = -1)
    {
        start = 0;
        int comment = beforeCaret.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            return null;
        if (ColourValues(beforeCaret, code, caretLine, out int colourStart) is { } colours)
        {
            start = colourStart;
            return colours;
        }

        if (ShapeValues(beforeCaret, code, out int shapeStart) is { } shapes)
        {
            start = shapeStart;
            return shapes;
        }

        if (EntrySnippets(beforeCaret, code, caretLine, out int entryStart, out bool typeWritten) is { } entries)
        {
            start = entryStart;
            if (typeWritten)
                return entries;
            List<Completion> rest = Words(beforeCaret, code, out _) ?? [];
            return [.. entries, .. rest.Where(c => entries.TrueForAll(e => e.Name != c.Name))];
        }

        // After a dot only members make sense, known or not.
        if (!AfterDot().IsMatch(beforeCaret))
            return Words(beforeCaret, code, out start);
        Match match = DotChain().Match(beforeCaret);
        if (!match.Success)
            return null;

        string[] chain = match.Groups[1].Value.Split('.').Select(part => part.Trim()).ToArray();
        // A dot after a number (1.5) or a call isn't a member access.
        if (match.Index > 0 && (char.IsDigit(beforeCaret[match.Index - 1]) || beforeCaret[match.Index - 1] == '.'))
            return null;
        if (Scope(chain, code) is not { } scope)
            return null;

        string typed = match.Groups[2].Value;
        start = beforeCaret.Length - typed.Length;
        List<Completion> members = Members(scope);
        var starting = members.Where(m => m.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase));
        var containing = members.Where(m => !m.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
                                           && m.Name.Contains(typed, StringComparison.OrdinalIgnoreCase));
        List<Completion> found = starting.Concat(containing).ToList();
        // Nothing left to offer once the typed name is complete and the only match.
        return found.Count == 0 || (found.Count == 1 && found[0].Name == typed) ? null : found;
    }

    /// <summary>
    /// shade and update, whole, when a top-level line starts with (part of) their name, or their
    /// return type and then part of it; never once the code declares them. <paramref name="start"/>
    /// is where the line's text begins; <paramref name="typeWritten"/> says the return type was typed,
    /// so nothing else fits there.
    /// </summary>
    private List<Completion>? EntrySnippets(string beforeCaret, IReadOnlyList<string> code, int caretLine, out int start, out bool typeWritten)
    {
        start = 0;
        typeWritten = false;
        Match match = EntryStart().Match(beforeCaret);
        if (caretLine < 0 || caretLine >= code.Count || !match.Success || Depth(code, caretLine) != 0)
            return null;
        string? type = match.Groups[2].Success ? match.Groups[2].Value : null;
        string typed = match.Groups[3].Value;
        var found = new List<Completion>();
        foreach ((string returns, string name, string header, string text, string doc) in Entries)
        {
            if ((type is not null && type != returns) || !name.StartsWith(typed, StringComparison.Ordinal) || name == typed
                || Declares(code, caretLine, name + @"\s*\("))
                continue;
            string written = name == "update" && !Declares(code, caretLine, @"struct\s+State\b") ? UpdateWithState : text;
            found.Add(new Completion(name, CompletionKind.Snippet, header, Doc: doc, Text: written));
        }

        if (found.Count == 0)
            return null;
        start = match.Groups[1].Length;
        typeWritten = type is not null;
        return found;
    }

    /// <summary>A colour's value being started: <c>colour c = |</c>, <c>c = |</c> for a colour c, or <c>[Default(|</c> on a colour uniform's line.</summary>
    [GeneratedRegex(@"(?:\bcolour\s+\w+\s*=|(?<![\w.])(\w+)\s*=|\[\s*Default\s*\()\s*(\w*)$")]
    private static partial Regex ColourValueStart();

    /// <summary>
    /// Where a colour is being given, the named colours (and colours.hsv, colours.hue) come up
    /// at once, each written in whole as colours.name; the usual words follow once something is typed.
    /// </summary>
    private List<Completion>? ColourValues(string beforeCaret, IReadOnlyList<string> code, int caretLine, out int start)
    {
        start = 0;
        Match match = ColourValueStart().Match(beforeCaret);
        if (!match.Success || !_scopes.TryGetValue("colours", out var named))
            return null;
        string line = caretLine >= 0 && caretLine < code.Count ? code[caretLine] : beforeCaret;
        bool colour = match.Groups[1].Success
            ? IsColour(match.Groups[1].Value, code)
            : !match.Value.TrimStart().StartsWith('[') || Regex.IsMatch(line, @"\buniform\s+colour\b");
        if (!colour || (match.Groups[1].Success && match.Value.Contains("==")))
            return null;

        string typed = match.Groups[2].Value;
        start = beforeCaret.Length - typed.Length;
        bool Fits(Completion c) => c.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
                                   || ("colours." + c.Name).StartsWith(typed, StringComparison.OrdinalIgnoreCase);
        var found = named.Where(c => c.Kind is CompletionKind.Constant or CompletionKind.Function && Fits(c))
            .Select(c => c with { Text = "colours." + c.Name + (c.Kind == CompletionKind.Function ? "($)" : "") })
            .ToList();
        if (typed.Length > 0 && Words(beforeCaret, code, out _) is { } words)
            found.AddRange(words.Where(w => found.TrueForAll(f => f.Name != w.Name)));
        return found.Count == 0 || found.Exists(f => f.Text == typed) ? null : found;
    }

    /// <summary>
    /// Where a value starts: after = (not ==), ( , ? : return, or an operator, with the start of a
    /// word or nothing typed yet.
    /// </summary>
    [GeneratedRegex(@"(?:(?<![=!<>])=|[(,?:+\-*/<>]|\breturn)\s*([A-Za-z_]\w*)?$")]
    private static partial Regex ValueStart();

    /// <summary>A function's declaration up to its open bracket: its parameters aren't values.</summary>
    [GeneratedRegex(@"^\s*(\w+)\s+\w+\s*\(")]
    private static partial Regex DeclarationOpen();

    /// <summary>
    /// Where a value starts, the library's shapes come up at once, each written in as Box(…) with the
    /// caret between the brackets; once something is typed the usual words follow them.
    /// </summary>
    private List<Completion>? ShapeValues(string beforeCaret, IReadOnlyList<string> code, out int start)
    {
        start = 0;
        Match match = ValueStart().Match(beforeCaret);
        if (!match.Success)
            return null;
        // Inside a declaration's parameter list (float4 shade(|) is not a value.
        Match declaration = DeclarationOpen().Match(beforeCaret);
        if (declaration.Success && (_typeNames.Contains(declaration.Groups[1].Value) || declaration.Groups[1].Value == "void")
            && beforeCaret[declaration.Length..].Count(c => c == '(') >= beforeCaret[declaration.Length..].Count(c => c == ')'))
            return null;

        string typed = match.Groups[1].Value;
        start = beforeCaret.Length - typed.Length;
        var found = Shapes().Where(s => s.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Select(s => s with { Text = s.Name + "($)" }).ToList();
        if (typed.Length > 0 && Words(beforeCaret, code, out _) is { } words)
            found.AddRange(words.Where(w => found.TrueForAll(f => f.Name != w.Name)));
        return found.Count == 0 || (typed.Length > 0 && found.Exists(f => f.Name == typed)) ? null : found;
    }

    /// <summary>The library's shapes (its top-level structs that are an IShape), found once.</summary>
    private List<Completion>? _shapes;

    private List<Completion> Shapes() =>
        _shapes ??= _globals.Where(g => g.Kind == CompletionKind.Type && _interfaces.TryGetValue(g.Name, out var faces) && faces.Contains("IShape"))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Whether the code declares a name as a colour (<c>colour c</c>, <c>uniform colour tint</c>).</summary>
    private static bool IsColour(string name, IReadOnlyList<string> code) =>
        code.Any(line => Regex.IsMatch(line, $@"\bcolour\s+{Regex.Escape(name)}\b"));

    /// <summary>How many braces are open at the start of a line.</summary>
    private static int Depth(IReadOnlyList<string> code, int line)
    {
        int depth = 0;
        for (int i = 0; i < line; i++)
        {
            int comment = code[i].IndexOf("//", StringComparison.Ordinal);
            foreach (char c in comment >= 0 ? code[i][..comment] : code[i])
                depth = c == '{' ? depth + 1 : c == '}' ? Math.Max(0, depth - 1) : depth;
        }

        return depth;
    }

    /// <summary>Whether a line other than the caret's has the pattern outside a comment.</summary>
    private static bool Declares(IReadOnlyList<string> code, int caretLine, string pattern)
    {
        for (int i = 0; i < code.Count; i++)
        {
            if (i == caretLine)
                continue;
            int comment = code[i].IndexOf("//", StringComparison.Ordinal);
            if (Regex.IsMatch(comment >= 0 ? code[i][..comment] : code[i], @"\b" + pattern))
                return true;
        }

        return false;
    }

    /// <summary>Completions for the word before the caret, or null where none fit.</summary>
    private List<Completion>? Words(string beforeCaret, IReadOnlyList<string> code, out int start)
    {
        start = 0;
        Match word = WordAtEnd().Match(beforeCaret);
        if (!word.Success)
            return null;

        // A new name is being given: after a type, let or var, nothing here can be it.
        Match before = WordBeforeSpace().Match(beforeCaret[..word.Index]);
        if (before.Success && (_typeNames.Contains(before.Groups[1].Value)
                               || before.Groups[1].Value is "let" or "var" or "struct" or "namespace" or "module" or "import"))
            return null;

        string typed = word.Groups[1].Value;
        start = word.Index;
        var all = new List<Completion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Offer(Completion completion)
        {
            if (seen.Add(completion.Name))
                all.Add(completion);
        }

        foreach (Completion own in CodeNames(code))
            Offer(own);
        foreach (Completion provided in _provided)
            Offer(provided);
        foreach (Completion global in _globals)
            Offer(global);
        foreach (string type in SlangSyntax.BuiltInTypes)
            Offer(new Completion(type, CompletionKind.Type, "type"));
        foreach ((string name, string detail, string doc) in Intrinsics)
            Offer(new Completion(name, CompletionKind.Function, detail, Doc: doc));
        foreach (string keyword in Keywords)
            Offer(new Completion(keyword, CompletionKind.Keyword, "keyword"));

        // Typed out already: nothing to finish, and Enter must stay a new line.
        if (seen.Contains(typed))
            return null;

        // The code's and the host's names first, then the rest, each alphabetically.
        static int Rank(Completion c) => c.Kind == CompletionKind.Variable ? 0 : 1;
        var starting = all.Where(c => c.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Rank).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
        // One or two letters inside a name match too much; three are worth it.
        IEnumerable<Completion> containing = typed.Length < 3 ? [] : all.Where(c => !c.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
                                                             && c.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Rank).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
        List<Completion> found = starting.Concat(containing).ToList();
        // "shape" (from three letters on) is a way in to every shape: they come first, written in whole.
        if (typed.Length >= 3 && "shapes".StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            found.InsertRange(0, Shapes().Select(shape => shape with { Text = shape.Name + "($)" }));
        return found.Count == 0 ? null : found;
    }

    /// <summary>
    /// What the name at a column is, for hovering: every declaration of it (overloads each), or null
    /// for no name or one nothing is known about. After a dot it is a member, found the way completion
    /// finds the scope; otherwise the code's, the host's, the library's or an intrinsic.
    /// <paramref name="start"/> is where the name begins.
    /// </summary>
    public List<Completion>? Describe(string line, int column, IReadOnlyList<string> code, out int start)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        start = column;
        if (column < 0 || column >= line.Length || !Word(line[column]))
            return null;
        while (start > 0 && Word(line[start - 1]))
            start--;
        int end = column;
        while (end < line.Length && Word(line[end]))
            end++;
        string name = line[start..end];
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        if (char.IsDigit(name[0]) || (comment >= 0 && comment < start))
            return null;

        string before = line[..start];
        if (AfterDot().IsMatch(before))
        {
            Match match = DotChain().Match(before);
            if (!match.Success || (match.Index > 0 && (char.IsDigit(before[match.Index - 1]) || before[match.Index - 1] == '.')))
                return null;
            string[] chain = match.Groups[1].Value.Split('.').Select(part => part.Trim()).ToArray();
            if (Scope(chain, code) is not { } scope)
                return null;
            if (_declared.TryGetValue((scope, name), out var own))
                return own;
            foreach (string face in _interfaces.TryGetValue(scope, out var faces) ? faces : [])
            {
                if (_declared.TryGetValue((face, name), out var shared))
                    return shared;
            }

            return null;
        }

        foreach (Completion own in CodeNames(code))
        {
            if (own.Name == name)
                return [own];
        }

        if (_provided.Find(p => p.Name == name) is { Name: not null } provided)
            return [provided];
        if (_declared.TryGetValue(("", name), out var global))
            return global;
        foreach ((string intrinsic, string detail, string doc) in Intrinsics)
        {
            if (intrinsic == name)
                return [new Completion(name, CompletionKind.Function, detail, Doc: doc)];
        }

        return null;
    }

    /// <summary>The names the code declares: uniforms, locals and parameters by their type, let and var, and functions.</summary>
    private IEnumerable<Completion> CodeNames(IReadOnlyList<string> code)
    {
        foreach (string line in code)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            string text = comment >= 0 ? line[..comment] : line;
            foreach (Match match in Declared().Matches(text))
            {
                string type = match.Groups[1].Value;
                if (!_typeNames.Contains(type))
                    continue;
                bool function = match.Groups[3].Value == "(";
                yield return new Completion(match.Groups[2].Value, function ? CompletionKind.Function : CompletionKind.Variable, function ? "→ " + type : type);
            }

            foreach (Match match in LetName().Matches(text))
                yield return new Completion(match.Groups[1].Value, CompletionKind.Variable, "let");
        }
    }

    /// <summary>The scope a dotted chain ends in: a namespace, then nested scopes or fields; or a variable's type, then fields.</summary>
    private string? Scope(string[] chain, IReadOnlyList<string> code)
    {
        string? scope = _namespaces.Contains(chain[0]) || (_scopes.ContainsKey(chain[0]) && !_memberTypes.ContainsKey(chain[0]))
            ? chain[0]
            : VariableType(chain[0], code);
        for (int i = 1; i < chain.Length && scope is not null; i++)
        {
            if (_scopes.ContainsKey(scope + "." + chain[i]))
                scope += "." + chain[i];
            else
                scope = _memberTypes.TryGetValue(scope, out var types) && types.TryGetValue(chain[i], out string? type) ? Resolve(scope, type) : null;
        }

        return scope;
    }

    /// <summary>
    /// A member's type as a scope: an alias becomes what it stands for, and a name declared
    /// in the member's own namespace (or one round it) is found there ("Blend" in blend → "blend.Blend").
    /// </summary>
    private string Resolve(string scope, string type)
    {
        type = _aliases.GetValueOrDefault(type, type);
        for (string outer = scope; outer.Length > 0; outer = outer.Contains('.') ? outer[..outer.LastIndexOf('.')] : "")
        {
            if (_scopes.ContainsKey(outer + "." + type))
                return outer + "." + type;
        }

        return type;
    }

    /// <summary>A name's type, from how the code declares it; only types the library knows.</summary>
    private string? VariableType(string name, IReadOnlyList<string> code)
    {
        foreach (string line in code)
        {
            foreach (Match match in TypedName().Matches(line))
            {
                string type = _aliases.GetValueOrDefault(match.Groups[1].Value, match.Groups[1].Value);
                if (match.Groups[2].Value == name && _scopes.ContainsKey(type))
                    return type;
            }

            Match let = LetConstructed().Match(line);
            string built = let.Success ? _aliases.GetValueOrDefault(let.Groups[2].Value, let.Groups[2].Value) : "";
            if (let.Success && let.Groups[1].Value == name && _scopes.ContainsKey(built))
                return built;
        }

        return null;
    }

    private List<Completion> Members(string scope)
    {
        var members = new List<Completion>(_scopes.TryGetValue(scope, out var own) ? own : []);
        if (_interfaces.TryGetValue(scope, out var interfaces))
        {
            foreach (string face in interfaces)
            {
                if (_scopes.TryGetValue(face, out var shared))
                    members.AddRange(shared.Where(m => !members.Exists(o => o.Name == m.Name)));
            }

            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        return members;
    }

    private void Read(string original, ShaderConstants constants)
    {
        // Comments become spaces, so every place in the code is the same in both and docs are found above a match.
        string source = Regex.Replace(original, @"/\*.*?\*/|//[^\n]*", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
        string? Doc(int index) => DocAbove(original, index);
        ReadGlobals(source, Doc);
        var spans = new List<(int Start, int End, string Scope, string Kind)>();
        foreach (Match match in Opener().Matches(source))
        {
            int end = Close(source, match.Index + match.Length - 1);
            string name = match.Groups[2].Value;
            string kind = match.Groups[1].Value;
            spans.Add((match.Index + match.Length, end, name, kind));
            if (kind == "struct" && match.Groups[3].Success)
                _interfaces[name] = match.Groups[3].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        foreach (Match match in ShapeExtension().Matches(source))
            spans.Add((match.Index + match.Length, Close(source, match.Index + match.Length - 1), match.Groups[1].Value, "extension"));
        foreach (Match match in TypeExtension().Matches(source))
            spans.Add((match.Index + match.Length, Close(source, match.Index + match.Length - 1), match.Groups[1].Value, "extension"));

        // A nested scope is named after its parents: an enum in namespace blend is "blend.Mode".
        var named = spans.Select(span =>
        {
            string full = span.Scope;
            foreach (var outer in spans)
            {
                if (outer.Kind == "namespace" && outer.Start < span.Start && outer.End > span.End)
                    full = outer.Scope + "." + span.Scope;
            }

            return (span.Start, span.End, Scope: full, span.Kind);
        }).ToList();

        foreach (var span in named)
        {
            Scope(span.Scope);
            if (span.Kind == "namespace")
                _namespaces.Add(span.Scope);
            // A struct or namespace inside a namespace is one of its members (blend.Blend, blend.colours).
            string outer = span.Scope.Contains('.') ? span.Scope[..span.Scope.LastIndexOf('.')] : "";
            if (outer.Length > 0 && span.Kind is "struct" or "namespace" or "interface")
            {
                string inner = span.Scope[(outer.Length + 1)..];
                int declared = source.LastIndexOf(span.Kind + " " + inner, span.Start, StringComparison.Ordinal);
                Add(outer, new Completion(inner, span.Kind == "namespace" ? CompletionKind.Namespace : CompletionKind.Type, span.Kind,
                    Doc: declared >= 0 ? Doc(declared) : null));
            }

            if (span.Kind == "enum")
            {
                string parent = outer;
                if (parent.Length > 0)
                    Add(parent, new Completion(span.Scope[(parent.Length + 1)..], CompletionKind.Type, "enum"));
                foreach (Match value in Regex.Matches(source[span.Start..span.End], @"(\w+)\s*(?:=[^,}]*)?,?"))
                    Add(span.Scope, new Completion(value.Groups[1].Value, CompletionKind.EnumValue, span.Scope, Doc: Doc(span.Start + value.Index)));
            }
        }

        string? Inner(int index) =>
            named.Where(span => span.Start <= index && index < span.End && span.Kind != "enum")
                .OrderBy(span => span.End - span.Start).Select(span => span.Scope).FirstOrDefault();

        foreach (Match match in Constant().Matches(source))
        {
            if (Inner(match.Index) is not { } scope)
                continue;
            string name = match.Groups[2].Value;
            Vector4? swatch = null;
            if (match.Groups[1].Value is "float3" or "float4" or "colour" && constants.TryGet(scope + "." + name, out float[] values) && values.Length >= 3)
                swatch = new Vector4(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1);
            Add(scope, new Completion(name, CompletionKind.Constant, match.Groups[1].Value, swatch, Doc(match.Index)));
            Type(scope, name, match.Groups[1].Value);
        }

        foreach (Match match in Property().Matches(source))
        {
            if (Inner(match.Index) is not { } scope)
                continue;
            Add(scope, new Completion(match.Groups[2].Value, CompletionKind.Field, match.Groups[1].Value, Doc: Doc(match.Index)));
            Type(scope, match.Groups[2].Value, match.Groups[1].Value);
        }

        foreach (Match match in Field().Matches(source))
        {
            if (Inner(match.Index) is not { } scope)
                continue;
            Add(scope, new Completion(match.Groups[2].Value, CompletionKind.Field, match.Groups[1].Value, Doc: Doc(match.Index)));
            Type(scope, match.Groups[2].Value, match.Groups[1].Value);
        }

        foreach (Match match in Method().Matches(source))
        {
            if (Inner(match.Index) is not { } scope || match.Groups[2].Value == "__init")
                continue;
            bool inNamespace = named.Exists(span => span.Scope == scope && span.Kind == "namespace");
            string parameters = Regex.Replace(match.Groups[3].Value.Trim(), @"\s+", " ");
            Add(scope, new Completion(
                match.Groups[2].Value, inNamespace ? CompletionKind.Function : CompletionKind.Method,
                $"({parameters}) → {match.Groups[1].Value.Trim()}", Doc: Doc(match.Index)));
        }
    }

    /// <summary>The top-level names of one library file: what a word can complete to.</summary>
    private void ReadGlobals(string source, Func<int, string?> doc)
    {
        foreach (Match match in TypeAlias().Matches(source))
        {
            _typeNames.Add(match.Groups[1].Value);
            _aliases[match.Groups[1].Value] = match.Groups[2].Value;
            Global(new Completion(match.Groups[1].Value, CompletionKind.Type, match.Groups[2].Value, Doc: doc(match.Index)));
        }

        foreach (Match match in NamespaceName().Matches(source))
            Global(new Completion(match.Groups[1].Value, CompletionKind.Namespace, "namespace", Doc: doc(match.Index)));

        // Only what sits outside every brace is top level.
        int depth = 0;
        var top = new bool[source.Length + 1];
        for (int i = 0; i < source.Length; i++)
        {
            top[i] = depth == 0;
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
                depth = Math.Max(0, depth - 1);
        }

        foreach (Match match in StructName().Matches(source))
        {
            string name = match.Groups[1].Value;
            _typeNames.Add(name);
            // Generic structs (Rotated<S>, Union<A, B>) are what the shape changes return, never written out.
            if (!top[match.Index] || name.EndsWith("Attribute", StringComparison.Ordinal) || match.Groups[2].Value == "<")
                continue;
            Match init = Regex.Match(source[match.Index..Close(source, match.Index + match.Length - 1)], @"public\s+__init\s*\(([^)]*)\)");
            Global(new Completion(name, CompletionKind.Type, init.Success ? "(" + Regex.Replace(init.Groups[1].Value.Trim(), @"\s+", " ") + ")" : "struct", Doc: doc(match.Index)));
        }

        foreach (Match match in Constant().Matches(source))
        {
            if (top[match.Index] && !Hidden.Contains(match.Groups[2].Value))
                Global(new Completion(match.Groups[2].Value, CompletionKind.Constant, match.Groups[1].Value, Doc: doc(match.Index)));
        }

        foreach (Match match in Method().Matches(source))
        {
            string name = match.Groups[2].Value;
            if (!top[match.Index] || Hidden.Contains(name))
                continue;
            string parameters = Regex.Replace(match.Groups[3].Value.Trim(), @"\s+", " ");
            Global(new Completion(name, CompletionKind.Function, $"({parameters}) → {match.Groups[1].Value.Trim()}", Doc: doc(match.Index)));
        }
    }

    /// <summary>A top-level name: offered once, every declaration kept for hovering.</summary>
    private void Global(Completion completion)
    {
        Declare("", completion);
        if (!_globals.Exists(g => g.Name == completion.Name))
            _globals.Add(completion);
    }

    private void Declare(string scope, Completion completion)
    {
        if (!_declared.TryGetValue((scope, completion.Name), out var all))
            _declared[(scope, completion.Name)] = all = [];
        if (!all.Exists(c => c.Detail == completion.Detail))
            all.Add(completion);
    }

    /// <summary>
    /// The <c>///</c> lines just above the line a declaration starts on (attributes between are skipped), or null.
    /// Lines run together into paragraphs; a blank one starts the next; indented ones are code, kept as they are.
    /// </summary>
    private static string? DocAbove(string source, int index)
    {
        var lines = new List<string>();
        int end = index == 0 ? -1 : source.LastIndexOf('\n', index - 1);
        while (end > 0)
        {
            int start = source.LastIndexOf('\n', end - 1) + 1;
            string text = source[start..end].Trim();
            if (text.StartsWith("///", StringComparison.Ordinal))
                lines.Insert(0, text[3..].TrimEnd());
            else if (!(text.StartsWith('[') && lines.Count == 0))
                break;
            end = start - 1;
        }

        if (lines.Count == 0)
            return null;
        var parts = new List<string>();
        bool joining = false;
        foreach (string raw in lines)
        {
            string line = raw.StartsWith(' ') ? raw[1..] : raw;
            if (line.Trim().Length == 0)
            {
                parts.Add("");
                joining = false;
            }
            else if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                parts.Add(line);
                joining = false;
            }
            else if (joining)
                parts[^1] += " " + line.Replace("`", "");
            else
            {
                parts.Add(line.Replace("`", ""));
                joining = true;
            }
        }

        return string.Join('\n', parts).Trim('\n');
    }

    private void Scope(string scope)
    {
        if (!_scopes.ContainsKey(scope))
            _scopes[scope] = [];
    }

    private void Add(string scope, Completion completion)
    {
        Scope(scope);
        Declare(scope, completion);
        if (!_scopes[scope].Exists(existing => existing.Name == completion.Name))
            _scopes[scope].Add(completion);
    }

    private void Type(string scope, string member, string type)
    {
        if (!_memberTypes.TryGetValue(scope, out var types))
            _memberTypes[scope] = types = new Dictionary<string, string>(StringComparer.Ordinal);
        types[member] = type;
    }

    private static int Close(string source, int open)
    {
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return i;
        }

        return source.Length;
    }
}
