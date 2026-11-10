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
}

/// <summary>One thing completion offers: its name, what it is, a short signature or type, and a colour for colour constants.</summary>
internal readonly record struct Completion(string Name, CompletionKind Kind, string Detail, Vector4? Swatch = null);

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

    /// <summary>The interfaces each struct says it implements.</summary>
    private readonly Dictionary<string, List<string>> _interfaces = new(StringComparer.Ordinal);

    [GeneratedRegex(@"\b(namespace|struct|interface|enum)\s+(\w+)\s*(?::\s*([\w\s,<>]+?))?\s*\{")]
    private static partial Regex Opener();

    [GeneratedRegex(@"\bextension\s*<\s*\w+\s*:\s*(\w+)\s*>\s*\w+\s*\{")]
    private static partial Regex ShapeExtension();

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

    [GeneratedRegex(@"\b([A-Z]\w*)\s+(\w+)\s*(?:=|;|,|\))")]
    private static partial Regex TypedName();

    [GeneratedRegex(@"\blet\s+(\w+)\s*=\s*([A-Z]\w*)\s*\(")]
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

    /// <summary>The library's top-level names that are host plumbing, not for effects.</summary>
    private static readonly HashSet<string> Hidden = ["VERSION", "canvasOf", "input", "shading"];

    /// <summary>Keywords worth offering in an effect body.</summary>
    private static readonly string[] Keywords =
    [
        "if", "else", "for", "while", "return", "break", "continue", "discard", "let", "var", "const",
        "static", "uniform", "in", "out", "inout", "struct", "import", "true", "false",
    ];

    /// <summary>Slang's common intrinsics, with their usual parameters.</summary>
    private static readonly (string Name, string Detail)[] Intrinsics =
    [
        ("abs", "(x)"), ("acos", "(x)"), ("asin", "(x)"), ("atan", "(x)"), ("atan2", "(y, x)"), ("ceil", "(x)"),
        ("clamp", "(x, min, max)"), ("cos", "(x)"), ("cross", "(a, b)"), ("ddx", "(x)"), ("ddy", "(x)"),
        ("degrees", "(radians)"), ("distance", "(a, b)"), ("dot", "(a, b)"), ("exp", "(x)"), ("exp2", "(x)"),
        ("floor", "(x)"), ("fmod", "(x, y)"), ("frac", "(x)"), ("fwidth", "(x)"), ("length", "(v)"),
        ("lerp", "(a, b, t)"), ("log", "(x)"), ("log2", "(x)"), ("max", "(a, b)"), ("min", "(a, b)"),
        ("mul", "(a, b)"), ("normalize", "(v)"), ("pow", "(x, y)"), ("radians", "(degrees)"),
        ("reflect", "(i, n)"), ("round", "(x)"), ("rsqrt", "(x)"), ("saturate", "(x)"), ("sign", "(x)"),
        ("sin", "(x)"), ("smoothstep", "(edge0, edge1, x)"), ("sqrt", "(x)"), ("step", "(edge, x)"),
        ("tan", "(x)"), ("tanh", "(x)"), ("trunc", "(x)"),
    ];

    /// <summary>The library's top-level names: free functions and constants, structs, aliases, namespaces.</summary>
    private readonly List<Completion> _globals = new();

    /// <summary>Names the host declares for the code (the lab's time, mousePos, …).</summary>
    private readonly List<Completion> _provided = new();

    /// <summary>Every type name a declaration can start with: Slang's, the library's structs and aliases.</summary>
    private readonly HashSet<string> _typeNames = new(SlangSyntax.BuiltInTypes, StringComparer.Ordinal);

    /// <param name="provided">Names the host declares for the code, with their types.</param>
    public static SlangCompletions FromSources(
        IEnumerable<string> sources, ShaderConstants constants, IEnumerable<(string Name, string Type)>? provided = null)
    {
        var completions = new SlangCompletions();
        foreach (string source in sources)
            completions.Read(Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", ""), constants);
        foreach ((string name, string type) in provided ?? [])
            completions._provided.Add(new Completion(name, CompletionKind.Variable, type + " · provided"));
        foreach (List<Completion> members in completions._scopes.Values)
            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return completions;
    }

    /// <summary>
    /// What may follow the dot just before the caret, or complete the word
    /// being typed, filtered by what is typed; null when there is nothing to
    /// offer. <paramref name="start"/> is the column where the typed part begins.
    /// </summary>
    public List<Completion>? At(string beforeCaret, IReadOnlyList<string> code, out int start)
    {
        start = 0;
        int comment = beforeCaret.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            return null;
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
        foreach ((string name, string detail) in Intrinsics)
            Offer(new Completion(name, CompletionKind.Function, detail));
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
        return found.Count == 0 ? null : found;
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
        string? scope = _scopes.ContainsKey(chain[0]) && !_memberTypes.ContainsKey(chain[0]) ? chain[0] : VariableType(chain[0], code);
        for (int i = 1; i < chain.Length && scope is not null; i++)
        {
            if (_scopes.ContainsKey(scope + "." + chain[i]))
                scope += "." + chain[i];
            else
                scope = _memberTypes.TryGetValue(scope, out var types) && types.TryGetValue(chain[i], out string? type) ? type : null;
        }

        return scope;
    }

    /// <summary>A name's type, from how the code declares it; only types the library knows.</summary>
    private string? VariableType(string name, IReadOnlyList<string> code)
    {
        foreach (string line in code)
        {
            foreach (Match match in TypedName().Matches(line))
            {
                if (match.Groups[2].Value == name && _scopes.ContainsKey(match.Groups[1].Value))
                    return match.Groups[1].Value;
            }

            Match let = LetConstructed().Match(line);
            if (let.Success && let.Groups[1].Value == name && _scopes.ContainsKey(let.Groups[2].Value))
                return let.Groups[2].Value;
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

    private void Read(string source, ShaderConstants constants)
    {
        ReadGlobals(source);
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
            if (span.Kind == "enum")
            {
                string parent = span.Scope.Contains('.') ? span.Scope[..span.Scope.LastIndexOf('.')] : "";
                if (parent.Length > 0)
                    Add(parent, new Completion(span.Scope[(parent.Length + 1)..], CompletionKind.Type, "enum"));
                foreach (Match value in Regex.Matches(source[span.Start..span.End], @"(\w+)\s*(?:=[^,}]*)?,?"))
                    Add(span.Scope, new Completion(value.Groups[1].Value, CompletionKind.EnumValue, span.Scope));
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
            Add(scope, new Completion(name, CompletionKind.Constant, match.Groups[1].Value, swatch));
        }

        foreach (Match match in Property().Matches(source))
        {
            if (Inner(match.Index) is not { } scope)
                continue;
            Add(scope, new Completion(match.Groups[2].Value, CompletionKind.Field, match.Groups[1].Value));
            Type(scope, match.Groups[2].Value, match.Groups[1].Value);
        }

        foreach (Match match in Field().Matches(source))
        {
            if (Inner(match.Index) is not { } scope)
                continue;
            Add(scope, new Completion(match.Groups[2].Value, CompletionKind.Field, match.Groups[1].Value));
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
                $"({parameters}) → {match.Groups[1].Value.Trim()}"));
        }
    }

    /// <summary>The top-level names of one library file: what a word can complete to.</summary>
    private void ReadGlobals(string source)
    {
        foreach (Match match in TypeAlias().Matches(source))
        {
            _typeNames.Add(match.Groups[1].Value);
            _globals.Add(new Completion(match.Groups[1].Value, CompletionKind.Type, match.Groups[2].Value));
        }

        foreach (Match match in NamespaceName().Matches(source))
            _globals.Add(new Completion(match.Groups[1].Value, CompletionKind.Namespace, "namespace"));

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
            _globals.Add(new Completion(name, CompletionKind.Type, init.Success ? "(" + Regex.Replace(init.Groups[1].Value.Trim(), @"\s+", " ") + ")" : "struct"));
        }

        foreach (Match match in Constant().Matches(source))
        {
            if (top[match.Index] && !Hidden.Contains(match.Groups[2].Value))
                _globals.Add(new Completion(match.Groups[2].Value, CompletionKind.Constant, match.Groups[1].Value));
        }

        foreach (Match match in Method().Matches(source))
        {
            string name = match.Groups[2].Value;
            if (!top[match.Index] || Hidden.Contains(name) || _globals.Exists(g => g.Name == name))
                continue;
            string parameters = Regex.Replace(match.Groups[3].Value.Trim(), @"\s+", " ");
            _globals.Add(new Completion(name, CompletionKind.Function, $"({parameters}) → {match.Groups[1].Value.Trim()}"));
        }
    }

    private void Scope(string scope)
    {
        if (!_scopes.ContainsKey(scope))
            _scopes[scope] = [];
    }

    private void Add(string scope, Completion completion)
    {
        Scope(scope);
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
