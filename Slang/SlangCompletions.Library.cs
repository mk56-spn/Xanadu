using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The library side of completion: everything read once from the shader
/// library's source. Scopes and their members, the types of fields, aliases,
/// which structs implement which interfaces (so families can be listed), each
/// call's parameter types (so a call's argument can say what it expects), and
/// the doc comments with their tags.
/// </summary>
public sealed partial class SlangCompletions
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

    /// <summary>The library's interfaces (IShape, IGradient, …): each is a family of the structs that implement it.</summary>
    private readonly HashSet<string> _interfaceNames = new(StringComparer.Ordinal);

    /// <summary>
    /// What each family can make through a method, from its interface's <c>/// @fits</c> tag
    /// (a shape's filled() is a float; a gradient's amount() a float, its coloured() a colour).
    /// </summary>
    private readonly Dictionary<string, string[]> _fits = new(StringComparer.Ordinal);

    /// <summary>Each call's overloads by scope ("" for top level, a struct for its constructors) and name: their parameter types, generic ones as their constraint.</summary>
    private readonly Dictionary<(string Scope, string Name), List<string[]>> _parameters = new();

    /// <summary>The library's top-level names: free functions and constants, structs, aliases, namespaces.</summary>
    private readonly List<Completion> _globals = new();

    /// <summary>Names the host declares for the code (the lab's time, mouse, …).</summary>
    private readonly List<Completion> _provided = new();

    /// <summary>Every type name a declaration can start with: Slang's, the library's structs and aliases.</summary>
    private readonly HashSet<string> _typeNames = new(SlangSyntax.BuiltInTypes, StringComparer.Ordinal);

    /// <summary>The library's type aliases and what they stand for (colour → float4), so an alias finds its type's members.</summary>
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);

    [GeneratedRegex(@"\b(namespace|struct|interface|enum)\s+(\w+)\s*(?::\s*([\w\s,<>]+?))?\s*\{")]
    private static partial Regex Opener();

    /// <summary>An extension of every type with an interface (<c>extension&lt;P : IPlaced&gt; P {</c>): its type parameter and the interface.</summary>
    [GeneratedRegex(@"\bextension\s*<\s*(\w+)\s*:\s*(\w+)\s*>\s*\w+\s*\{")]
    private static partial Regex ShapeExtension();

    /// <summary>
    /// The name each interface's extension calls the type it extends (P for IPlaced, S for IShape), so
    /// a method returning P is shown returning the receiver's own type (rotated → Circle).
    /// </summary>
    private readonly Dictionary<string, string> _typeParameters = new(StringComparer.Ordinal);

    /// <summary>An extension of one concrete type (<c>extension float4 {</c>): its members are that type's.</summary>
    [GeneratedRegex(@"\bextension\s+(\w+)\s*\{")]
    private static partial Regex TypeExtension();

    [GeneratedRegex(@"public\s+static\s+const\s+(\w+)\s+(\w+)\s*=")]
    private static partial Regex Constant();

    [GeneratedRegex(@"public\s+property\s+(\w+)\s+(\w+)")]
    private static partial Regex Property();

    [GeneratedRegex(@"public\s+(?!static\b|property\b|struct\b|enum\b|namespace\b|interface\b|extension\b)([\w<>]+)\s+(\w+)\s*;")]
    private static partial Regex Field();

    /// <summary>A public function or method: its return type, name, generic parameters (<c>S : IShape</c>) and parameters.</summary>
    [GeneratedRegex(@"public\s+(?!static\b|property\b)([\w<>, ]+?)\s+(\w+)\s*(?:<([^>()]*)>)?\s*\(([^)]*)\)")]
    private static partial Regex Method();
    [GeneratedRegex(@"public\s+typealias\s+(\w+)\s*=\s*(\w+)")]
    private static partial Regex TypeAlias();

    [GeneratedRegex(@"public\s+namespace\s+(\w+)")]
    private static partial Regex NamespaceName();

    [GeneratedRegex(@"public\s+struct\s+(\w+)\s*(<?)[^{]*\{")]
    private static partial Regex StructName();

    /// <summary>
    /// Gives every type its interfaces' interfaces too (IShape is an IPlaced, so a
    /// Circle shares IPlaced's rotated and moved), once every source is read.
    /// </summary>
    private void InheritInterfaces()
    {
        foreach (string type in _interfaces.Keys.ToList())
        {
            var all = new List<string>();
            var pending = new Queue<string>(_interfaces[type]);
            while (pending.TryDequeue(out string? face))
            {
                if (all.Contains(face))
                    continue;
                all.Add(face);
                foreach (string inherited in _interfaces.TryGetValue(face, out var bases) ? bases : [])
                    pending.Enqueue(inherited);
            }

            _interfaces[type] = all;
        }
    }

    private void Read(string original, ShaderConstants constants)
    {
        // Comments become spaces, so every place in the code is the same in both and docs are found above a match.
        string source = Regex.Replace(original, @"/\*.*?\*/|//[^\n]*", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
        string? Doc(int index) => DocAbove(original, index);
        ReadGlobals(source, Doc, index => Tagged(original, index, "hidden"));
        var spans = new List<(int Start, int End, string Scope, string Kind)>();
        foreach (Match match in Opener().Matches(source))
        {
            int end = Close(source, match.Index + match.Length - 1);
            string name = match.Groups[2].Value;
            string kind = match.Groups[1].Value;
            spans.Add((match.Index + match.Length, end, name, kind));
            if (kind == "interface")
            {
                _interfaceNames.Add(name);
                if (TagValue(original, match.Index, "fits") is { } fits)
                    _fits[name] = fits.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            if (kind is "struct" or "interface" && match.Groups[3].Success)
                _interfaces[name] = match.Groups[3].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        foreach (Match match in ShapeExtension().Matches(source))
        {
            _typeParameters[match.Groups[2].Value] = match.Groups[1].Value;
            spans.Add((match.Index + match.Length, Close(source, match.Index + match.Length - 1), match.Groups[2].Value, "extension"));
        }
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
            string parameters = Regex.Replace(match.Groups[4].Value.Trim(), @"\s+", " ");
            Parameters(scope, match.Groups[2].Value, match.Groups[3].Value, parameters);
            Add(scope, new Completion(
                match.Groups[2].Value, inNamespace ? CompletionKind.Function : CompletionKind.Method,
                $"({parameters}) → {Constrained(match.Groups[1].Value.Trim(), match.Groups[3].Value)}", Doc: Doc(match.Index)));
        }
    }

    /// <summary>The top-level names of one library file: what a word can complete to.</summary>
    private void ReadGlobals(string source, Func<int, string?> doc, Func<int, bool> hidden)
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
            MatchCollection inits = Regex.Matches(source[match.Index..Close(source, match.Index + match.Length - 1)], @"public\s+__init\s*\(([^)]*)\)");
            foreach (Match each in inits)
                Parameters("", name, "", Regex.Replace(each.Groups[1].Value.Trim(), @"\s+", " "));
            Global(new Completion(name, CompletionKind.Type, inits.Count > 0 ? "(" + Regex.Replace(inits[0].Groups[1].Value.Trim(), @"\s+", " ") + ")" : "struct", Doc: doc(match.Index)));
        }

        foreach (Match match in Constant().Matches(source))
        {
            if (top[match.Index] && !hidden(match.Index))
                Global(new Completion(match.Groups[2].Value, CompletionKind.Constant, match.Groups[1].Value, Doc: doc(match.Index)));
        }

        foreach (Match match in Method().Matches(source))
        {
            string name = match.Groups[2].Value;
            if (!top[match.Index] || hidden(match.Index))
                continue;
            string parameters = Regex.Replace(match.Groups[4].Value.Trim(), @"\s+", " ");
            Parameters("", name, match.Groups[3].Value, parameters);
            Global(new Completion(name, CompletionKind.Function, $"({parameters}) → {Constrained(match.Groups[1].Value.Trim(), match.Groups[3].Value)}", Doc: doc(match.Index)));
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
            {
                // Tags (/// @hidden) steer completion; they're not for reading.
                if (!text[3..].TrimStart().StartsWith('@'))
                    lines.Insert(0, text[3..].TrimEnd());
            }
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

    /// <summary>
    /// Whether the <c>///</c> lines above a declaration carry <c>@tag</c>. The tags:
    /// <c>@hidden</c>, host plumbing never offered (it still describes on hover).
    /// </summary>
    private static bool Tagged(string source, int index, string tag) => TagValue(source, index, tag) is not null;

    /// <summary>
    /// What follows <c>@tag</c> in the <c>///</c> lines above a declaration ("" for a bare tag), or null
    /// without it. The tags: <c>@hidden</c> (never offered), <c>@fits types</c> on an interface (its
    /// family is offered where those types are wanted).
    /// </summary>
    private static string? TagValue(string source, int index, string tag)
    {
        int end = index == 0 ? -1 : source.LastIndexOf('\n', index - 1);
        while (end > 0)
        {
            int start = source.LastIndexOf('\n', end - 1) + 1;
            string text = source[start..end].Trim();
            if (!text.StartsWith("///", StringComparison.Ordinal) && !text.StartsWith('['))
                return null;
            Match found = Regex.Match(text, @"^///\s*@" + tag + @"\b(.*)$");
            if (found.Success)
                return found.Groups[1].Value.Trim();
            end = start - 1;
        }

        return null;
    }

    /// <summary>
    /// Keeps one overload's parameter types (<c>S shape, colour top = …</c> → S, colour), a generic one
    /// as its constraint (<c>S : IShape</c> makes S an IShape), so a call's argument knows what it wants.
    /// </summary>
    private void Parameters(string scope, string name, string generics, string parameters)
    {
        var constraints = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match generic in Regex.Matches(generics, @"(\w+)\s*:\s*(\w+)"))
            constraints[generic.Groups[1].Value] = generic.Groups[2].Value;
        string[] types = SplitArguments(parameters)
            .Select(p => Regex.Match(p, @"^\s*(?:(?:in|out|inout|const)\s+)*([A-Za-z_][\w<>]*)").Groups[1].Value)
            .Select(type => constraints.GetValueOrDefault(type, type))
            .ToArray();
        if (!_parameters.TryGetValue((scope, name), out var overloads))
            _parameters[(scope, name)] = overloads = [];
        overloads.Add(types);
    }

    /// <summary>A return type with the method's own generic names as their constraints: <c>Union&lt;S, T&gt;</c> for <c>T : IShape</c> → <c>Union&lt;S, IShape&gt;</c>.</summary>
    private static string Constrained(string type, string generics)
    {
        foreach (Match generic in Regex.Matches(generics, @"(\w+)\s*:\s*(\w+)"))
            type = Regex.Replace(type, $@"\b{generic.Groups[1].Value}\b", generic.Groups[2].Value);
        return type;
    }

    /// <summary>
    /// A member an interface gives, as <paramref name="receiver"/> has it: what it returns named after
    /// the receiver (an IPlaced's rotated returns P; a Circle's returns a Circle). The interface
    /// itself stands in when the receiver is no more than that.
    /// </summary>
    private Completion AsMemberOf(Completion member, string face, string receiver)
    {
        int arrow = member.Detail.LastIndexOf("→ ", StringComparison.Ordinal);
        if (arrow < 0 || !_typeParameters.TryGetValue(face, out string? parameter))
            return member;
        string returns = Regex.Replace(member.Detail[(arrow + 2)..], $@"\b{parameter}\b", receiver);
        return member with { Detail = member.Detail[..(arrow + 2)] + returns };
    }

    /// <summary>Text split at its top-level commas (none inside brackets); empty text has no parts.</summary>
    private static List<string> SplitArguments(string text)
    {
        var parts = new List<string>();
        int depth = 0, from = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '(' or '[' or '{' or '<')
                depth++;
            else if (c is ')' or ']' or '}' or '>')
                depth = Math.Max(0, depth - 1);
            else if (c == ',' && depth == 0)
            {
                parts.Add(text[from..i]);
                from = i + 1;
            }
        }

        if (text[from..].Trim().Length > 0 || parts.Count > 0)
            parts.Add(text[from..]);
        return parts;
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
}
