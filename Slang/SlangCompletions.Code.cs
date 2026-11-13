using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The code side of completion: what the code being edited declares (its own
/// structs, locals, functions, the kept State), and walking a dotted chain to
/// the scope it ends in, through the library's and the code's types.
/// </summary>
public sealed partial class SlangCompletions
{
    /// <summary>A dotted chain before the caret; an index on a part (collapsed to <c>[]</c> by <see cref="Collapsed"/>) is kept on it.</summary>
    [GeneratedRegex(@"([A-Za-z_]\w*(?:\[\])?(?:\s*\.\s*[A-Za-z_]\w*(?:\[\])?)*)\s*\.\s*(\w*)$")]
    private static partial Regex DotChain();

    /// <summary>The text with every bracketed index emptied (<c>s.points[s.at + 1]</c> → <c>s.points[]</c>), its end kept where it was relative to the caret.</summary>
    private static string Collapsed(string text)
    {
        for (string before = ""; before != text;)
        {
            before = text;
            text = Regex.Replace(text, @"\[[^\[\]]*\]", "\u0001");
        }

        return text.Replace("\u0001", "[]");
    }

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

    /// <summary>The names the code declares: uniforms, locals and parameters by their type, let and var, and functions.</summary>
    private IEnumerable<Completion> CodeNames(IReadOnlyList<string> code)
    {
        foreach (Completion type in _code.Types)
            yield return type;
        if (KeepsState(code))
            yield return new Completion(KeptState, CompletionKind.Variable, "State · kept",
                Doc: "What update left this frame, read-only here: update changes it, shade reads it.");
        foreach (string line in code)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            string text = comment >= 0 ? line[..comment] : line;
            foreach (Match match in Declared().Matches(text))
            {
                string type = match.Groups[1].Value;
                if (!IsTypeName(type))
                    continue;
                bool function = match.Groups[3].Value == "(";
                if (!function)
                {
                    yield return new Completion(match.Groups[2].Value, CompletionKind.Variable, type);
                    continue;
                }

                // A function: its parameters up to the bracket that closes them, then what it returns.
                int open = match.Index + match.Length - 1, depth = 0, close = text.Length;
                for (int i = open; i < text.Length; i++)
                {
                    depth += text[i] == '(' ? 1 : text[i] == ')' ? -1 : 0;
                    if (depth == 0)
                    {
                        close = i;
                        break;
                    }
                }

                string parameters = Regex.Replace(text[(open + 1)..close].Trim(), @"\s+", " ");
                yield return new Completion(match.Groups[2].Value, CompletionKind.Function, $"({parameters}) → {type}");
            }

            foreach (Match match in LetName().Matches(text))
            {
                // let takes the type it's built as, when that's a call to make one (let b = Box(…)).
                Match built = LetConstructed().Match(text[match.Index..]);
                string type = built.Success && built.Groups[1].Value == match.Groups[1].Value && IsTypeName(built.Groups[2].Value)
                    ? built.Groups[2].Value
                    : "let";
                yield return new Completion(match.Groups[1].Value, CompletionKind.Variable, type);
            }
        }
    }

    /// <summary>The scope a dotted chain ends in: a namespace, then nested scopes or fields; or a variable's type, then fields.</summary>
    private string? Scope(string[] chain, IReadOnlyList<string> code)
    {
        string? scope = _namespaces.Contains(chain[0]) || (_scopes.ContainsKey(chain[0]) && !_memberTypes.ContainsKey(chain[0]))
            ? chain[0]
            : VariableType(chain[0], code) ?? LibraryType(chain[0]);
        for (int i = 1; i < chain.Length && scope is not null; i++)
        {
            bool indexed = chain[i].EndsWith("[]", StringComparison.Ordinal);
            string part = indexed ? chain[i][..^2] : chain[i];
            if (!indexed && _scopes.ContainsKey(scope + "." + part))
                scope += "." + part;
            else if (TryMemberTypes(scope, out var types) && types.TryGetValue(part, out string? type))
            {
                // An array walks on only through an index, to its element; anything else indexed is unknown.
                bool array = type.EndsWith("[]", StringComparison.Ordinal);
                scope = array == indexed ? Resolve(scope, array ? type[..^2] : type) : null;
            }
            else
                scope = null;
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

    /// <summary>What shade reads update's State as: the compiler declares it, the code never does.</summary>
    private const string KeptState = "state";

    /// <summary>Whether the code keeps state: it declares update and its State struct, so shade has <c>state</c>.</summary>
    private bool KeepsState(IReadOnlyList<string> code) =>
        _code.Scopes.ContainsKey("State") && code.Any(line => Regex.IsMatch(line, @"\bvoid\s+update\s*\("));

    /// <summary>A name's type, from how the code declares it; only types the library or the code's own structs know.</summary>
    private string? VariableType(string name, IReadOnlyList<string> code)
    {
        if (name == KeptState && KeepsState(code))
            return "State";
        // What the host provides (mouse is a float2), unless the code declares the name itself.
        string? provided = _provided.Find(p => p.Name == name).Detail?.Split(' ')[0];
        foreach (string line in code)
        {
            foreach (Match match in TypedName().Matches(line))
            {
                string type = _aliases.GetValueOrDefault(match.Groups[1].Value, match.Groups[1].Value);
                if (match.Groups[2].Value == name && HasScope(type))
                    return type;
            }

            Match let = LetConstructed().Match(line);
            string built = let.Success ? _aliases.GetValueOrDefault(let.Groups[2].Value, let.Groups[2].Value) : "";
            if (let.Success && let.Groups[1].Value == name && HasScope(built))
                return built;
        }

        return provided is not null && HasScope(provided) ? _aliases.GetValueOrDefault(provided, provided) : null;
    }

    private List<Completion> Members(string scope)
    {
        var members = new List<Completion>(TryScope(scope, out var own) ? own : []);
        if (Vector(scope) is { } vector)
        {
            members.AddRange(vector.Members.Where(m => !members.Exists(o => o.Name == m.Name)));
            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (Faces(scope) is { Count: > 0 } interfaces)
        {
            foreach (string face in interfaces)
            {
                if (_scopes.TryGetValue(face, out var shared))
                    members.AddRange(shared.Where(m => !members.Exists(o => o.Name == m.Name)).Select(m => AsMemberOf(m, face, scope)));
            }

            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        return members;
    }

    /// <summary>
    /// The structs the code being edited declares (<c>struct State { float2 at; … }</c>), read again
    /// whenever the code changes: their fields, properties and methods, public or not, with their
    /// docs. A name the library already has stays the library's.
    /// </summary>
    private sealed class CodeStructs
    {
        public static readonly CodeStructs None = new();
        public string Text = "";
        public readonly List<Completion> Types = [];
        public readonly Dictionary<string, List<Completion>> Scopes = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Dictionary<string, string>> MemberTypes = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<string>> Interfaces = new(StringComparer.Ordinal);
        public readonly Dictionary<(string Scope, string Name), List<Completion>> Declared = new();
    }

    private CodeStructs _code = CodeStructs.None;

    [GeneratedRegex(@"\bstruct\s+([A-Za-z_]\w*)\s*(?::\s*([\w\s,<>]+?))?\s*\{")]
    private static partial Regex CodeStruct();

    /// <summary>
    /// A field: modifiers, a type (an array size after it allowed: <c>float2[8] points</c>), one or
    /// more names (an array size or a value allowed after one), then ;.
    /// </summary>
    [GeneratedRegex(@"(?:\b(?:public|private|internal|static|const)\s+)*\b([A-Za-z_][\w<>]*)(\s*\[[^\]]*\])?\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*(\[[^\]]*\])?\s*(?:=[^;{}]*)?;")]
    private static partial Regex CodeField();

    [GeneratedRegex(@"\bproperty\s+([\w<>]+)\s+(\w+)")]
    private static partial Regex CodeProperty();

    [GeneratedRegex(@"(?:\b(?:public|private|internal|static)\s+)*\b([A-Za-z_][\w<>]*)\s+([A-Za-z_]\w*)\s*\(([^)]*)\)\s*\{")]
    private static partial Regex CodeMethod();

    /// <summary>Words that start a statement, never a field's type.</summary>
    private static readonly HashSet<string> NotTypes = ["return", "let", "var", "typedef", "typealias", "property", "import", "using", "else"];

    /// <summary>Reads the code's structs, unless the code is what was read last.</summary>
    private void Learn(IReadOnlyList<string> code)
    {
        string text = string.Join('\n', code);
        if (text == _code.Text)
            return;
        var found = new CodeStructs { Text = text };
        // Comments become spaces, so places match the original and docs are found above a match.
        string source = Regex.Replace(text, @"/\*.*?\*/|//[^\n]*", m => Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);
        foreach (Match match in CodeStruct().Matches(source))
        {
            string name = match.Groups[1].Value;
            if (_scopes.ContainsKey(name) || found.Scopes.ContainsKey(name))
                continue;
            int open = match.Index + match.Length - 1;
            int end = Close(source, open);
            var members = found.Scopes[name] = [];
            var types = found.MemberTypes[name] = new Dictionary<string, string>(StringComparer.Ordinal);
            found.Types.Add(new Completion(name, CompletionKind.Type, "struct", Doc: DocAbove(text, match.Index)));
            if (match.Groups[2].Success)
                found.Interfaces[name] = match.Groups[2].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            // Only what sits straight inside the struct's braces is a member; method bodies are not.
            string body = source[(open + 1)..end];
            var outside = new bool[body.Length + 1];
            int depth = 0;
            for (int i = 0; i < body.Length; i++)
            {
                outside[i] = depth == 0;
                depth = body[i] == '{' ? depth + 1 : body[i] == '}' ? Math.Max(0, depth - 1) : depth;
            }

            void Member(Completion member, int at, string? type)
            {
                member = member with { Doc = DocAbove(text, open + 1 + at) };
                if (!found.Declared.TryGetValue((name, member.Name), out var all))
                    found.Declared[(name, member.Name)] = all = [];
                all.Add(member);
                if (!members.Exists(m => m.Name == member.Name))
                    members.Add(member);
                if (type is not null)
                    types[member.Name] = type;
            }

            foreach (Match field in CodeField().Matches(body))
            {
                string type = field.Groups[1].Value;
                if (!outside[field.Index] || NotTypes.Contains(type))
                    continue;
                // An array's type is its element's and "[]": a chain only walks on through an index.
                string size = field.Groups[2].Success ? field.Groups[2].Value : field.Groups[4].Value;
                string shown = type + Regex.Replace(size, @"\s+", "");
                string walked = size.Length > 0 ? type + "[]" : type;
                foreach (string each in field.Groups[3].Value.Split(','))
                    Member(new Completion(each.Trim(), CompletionKind.Field, shown), field.Index, walked);
            }

            foreach (Match property in CodeProperty().Matches(body))
            {
                if (outside[property.Index])
                    Member(new Completion(property.Groups[2].Value, CompletionKind.Field, property.Groups[1].Value), property.Index, property.Groups[1].Value);
            }

            foreach (Match method in CodeMethod().Matches(body))
            {
                string returns = method.Groups[1].Value;
                if (!outside[method.Index] || NotTypes.Contains(returns) || method.Groups[2].Value == "__init")
                    continue;
                string parameters = Regex.Replace(method.Groups[3].Value.Trim(), @"\s+", " ");
                Member(new Completion(method.Groups[2].Value, CompletionKind.Method, $"({parameters}) → {returns}"), method.Index, null);
            }

            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        _code = found;
    }

    private bool HasScope(string scope) => _scopes.ContainsKey(scope) || _code.Scopes.ContainsKey(scope) || Vector(scope) is not null;

    private bool TryScope(string scope, out List<Completion> members) =>
        _scopes.TryGetValue(scope, out members!) || _code.Scopes.TryGetValue(scope, out members!);

    private bool TryMemberTypes(string scope, out Dictionary<string, string> types)
    {
        if (_memberTypes.TryGetValue(scope, out types!) || _code.MemberTypes.TryGetValue(scope, out types!))
            return true;
        if (Vector(scope) is not { } vector)
            return false;
        types = vector.Types;
        return true;
    }

    /// <summary>A vector type's name: its element type and width (<c>float2</c> → float, 2).</summary>
    [GeneratedRegex(@"^(float|int|uint|half|double|bool)([234])$")]
    private static partial Regex VectorName();

    private readonly Dictionary<string, (List<Completion> Members, Dictionary<string, string> Types)?> _vectors = new(StringComparer.Ordinal);

    /// <summary>
    /// What a vector has (an alias like colour resolved): each component (x y z w, and r g b a from three on),
    /// and the swizzles most code uses (xy, xyz, rgb). Null for anything that isn't a vector.
    /// </summary>
    private (List<Completion> Members, Dictionary<string, string> Types)? Vector(string scope)
    {
        if (_vectors.TryGetValue(scope, out var known))
            return known;
        Match match = VectorName().Match(_aliases.GetValueOrDefault(scope, scope));
        if (!match.Success)
            return _vectors[scope] = null;

        string element = match.Groups[1].Value;
        int width = match.Groups[2].Value[0] - '0';
        var members = new List<Completion>();
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string name, string doc)
        {
            string type = name.Length == 1 ? element : element + name.Length;
            members.Add(new Completion(name, CompletionKind.Field, type, Doc: doc));
            types[name] = type;
        }

        string[] axes = ["x", "y", "z", "w"], channels = ["r", "g", "b", "a"];
        for (int i = 0; i < width; i++)
            Add(axes[i], $"Component {i + 1} of {width}.");
        if (width >= 3)
        {
            for (int i = 0; i < width; i++)
                Add(channels[i], $"Component {i + 1} of {width}, as a colour channel.");
            Add("xy", "The first two components.");
            Add("rgb", "The colour without its alpha.");
        }

        if (width == 4)
            Add("xyz", "The first three components.");
        members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return _vectors[scope] = (members, types);
    }

    /// <summary>A type a declaration can start with: Slang's, the library's, or one of the code's structs.</summary>
    private bool IsTypeName(string name) => _typeNames.Contains(name) || _code.Scopes.ContainsKey(name);

    /// <summary>The interfaces a type implements, with theirs; a code struct's come from what it names.</summary>
    private List<string> Faces(string scope)
    {
        if (_interfaces.TryGetValue(scope, out var library))
            return library;
        if (!_code.Interfaces.TryGetValue(scope, out var named))
            return [];
        var all = new List<string>();
        foreach (string face in named)
        {
            if (!all.Contains(face))
                all.Add(face);
            foreach (string inherited in _interfaces.TryGetValue(face, out var bases) ? bases : [])
            {
                if (!all.Contains(inherited))
                    all.Add(inherited);
            }
        }

        return all;
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