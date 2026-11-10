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
}

/// <summary>One thing that can follow a dot: its name, what it is, a short signature or type, and a colour for colour constants.</summary>
internal readonly record struct Completion(string Name, CompletionKind Kind, string Detail, Vector4? Swatch = null);

/// <summary>
/// Primitive completion after a dot, from the shader library's own source:
/// a namespace's members (<c>colours.</c>), a struct's fields, properties and
/// methods (<c>i.canvas.</c>), the shape methods every <c>IShape</c> gets, and
/// enum values (<c>blend.Mode.</c>). What a name before the dot is comes from
/// a light scan of the code being edited: <c>ItemInput i</c>,
/// <c>Canvas c = …</c>, <c>let b = Box(…)</c>. No calls in the chain, no
/// overloads, no inference beyond that.
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

    public static SlangCompletions FromSources(IEnumerable<string> sources, ShaderConstants constants)
    {
        var completions = new SlangCompletions();
        foreach (string source in sources)
            completions.Read(Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", ""), constants);
        foreach (List<Completion> members in completions._scopes.Values)
            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return completions;
    }

    /// <summary>
    /// What may follow the dot just before the caret, filtered by what is typed
    /// after it; null when the caret isn't after a dot whose left side is known.
    /// <paramref name="start"/> is the column where the typed part begins.
    /// </summary>
    public List<Completion>? At(string beforeCaret, IReadOnlyList<string> code, out int start)
    {
        start = 0;
        int comment = beforeCaret.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            return null;
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
