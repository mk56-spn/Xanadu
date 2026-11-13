using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The rules: each looks at the <see cref="Caret"/> and offers candidates at a
/// <see cref="Tier"/>; <see cref="Ranked"/> orders them. Snippets (whole
/// functions and statements), what the value wants (named colours for a
/// colour, a family's members for an interface, the struct for a struct;
/// shapes when nothing says), families (typing an interface's word lists its
/// structs), plain words, and members after a dot.
/// </summary>
public sealed partial class SlangCompletions
{
    /// <summary>What a value with no known type is offered at once: the shapes, the commonest value an effect builds.</summary>
    private const string DefaultFamily = "IShape";

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

    /// <summary>A line inside a body so far: its indent, then the one word being typed.</summary>
    [GeneratedRegex(@"^(\s*)([A-Za-z_]\w*)$")]
    private static partial Regex StatementStart();

    /// <summary>
    /// Statements offered whole: the keyword, its header, the text written in ($ is the caret,
    /// and every line after the first gets the indent of the line typed on), and whether it goes
    /// at the top level (a uniform) or inside a body (an if, a for).
    /// </summary>
    private static readonly (string Name, string Header, string Text, bool TopLevel)[] Statements =
    [
        ("if", "if (condition)", "if ($)\n{\n    \n}", false),
        ("for", "for (int i = 0; i < n; i++)", "for (int i = 0; i < $; i++)\n{\n    \n}", false),
        ("uniform", "[Default(1)] uniform float name;", "[Default(1)] uniform float $;", true),
        ("uniform", "[Default(colours.white)] uniform colour name;", "[Default(colours.white)] uniform colour $;", true),
    ];

    /// <summary>
    /// shade and update, and the statements, for a line being started. True when they are all
    /// that fits (a return type was typed, so the line is a function's start); then
    /// <paramref name="lineStart"/> is where the line's text begins, which the snippet replaces.
    /// </summary>
    private bool OfferSnippets(Caret caret, IReadOnlyList<string> code, List<Offer> offers, out int lineStart)
    {
        lineStart = caret.Start;
        List<Completion>? entries = EntrySnippets(caret.Before, code, caret.Line, out int entryStart, out bool typeWritten);
        if (entries is not null && typeWritten)
        {
            lineStart = entryStart;
            offers.AddRange(entries.Select(e => new Offer(e, Tier.Snippet, Fit.Prefix)));
            return true;
        }

        List<Completion>? statements = StatementSnippets(caret.Before, code, caret.Line, out _);
        foreach (Completion snippet in (IEnumerable<Completion>)[.. entries ?? [], .. statements ?? []])
            offers.Add(new Offer(snippet, Tier.Snippet, Fit.Prefix));
        return false;
    }

    /// <summary>
    /// Where a value starts, what makes the type it wants comes up at once, written in whole:
    /// named colours as colours.name for a colour, every member of a family for an interface,
    /// the struct itself for a struct; then the families whose <c>@fits</c> names the type (gradients
    /// for a colour, shapes and gradients for a float). When nothing says what is wanted, the
    /// shapes, then every family that fits anything: too much beats nothing.
    /// </summary>
    private void OfferExpected(Caret caret, List<Offer> offers)
    {
        if (!caret.Value || caret.NewName)
            return;
        IEnumerable<string> wants = caret.Wants.Count > 0 ? caret.Wants : [DefaultFamily];
        foreach ((string face, string[] fits) in _fits)
        {
            if (caret.Wants.Count > 0 ? !caret.Wants.Any(fits.Contains) : face == DefaultFamily)
                continue;
            foreach (Completion member in Family(face))
                offers.Add(new Offer(member, Tier.Fits, Matching(member.Name, caret.Typed, int.MaxValue)));
        }

        foreach (string type in wants)
        {
            if (type == "colour")
            {
                foreach (Completion named in _scopes.GetValueOrDefault("colours") ?? [])
                {
                    if (named.Kind is not (CompletionKind.Constant or CompletionKind.Function))
                        continue;
                    string text = "colours." + named.Name;
                    Fit fit = Matching(named.Name, caret.Typed, int.MaxValue);
                    if (text.StartsWith(caret.Typed, StringComparison.OrdinalIgnoreCase))
                        fit = Fit.Prefix;
                    offers.Add(new Offer(named with { Text = text + (named.Kind == CompletionKind.Function ? "($)" : "") }, Tier.Expected, fit));
                }
            }
            else if (_interfaceNames.Contains(type))
            {
                foreach (Completion member in Family(type))
                    offers.Add(new Offer(member, Tier.Expected, Matching(member.Name, caret.Typed, int.MaxValue)));
            }
            else if (_globals.Find(g => g.Kind == CompletionKind.Type && g.Name == type) is { Name: not null } global)
                offers.Add(new Offer(Called(global), Tier.Expected, Matching(type, caret.Typed, int.MaxValue)));
            else if (_code.Types.Find(t => t.Name == type) is { Name: not null } own)
                offers.Add(new Offer(Called(own), Tier.Expected, Matching(type, caret.Typed, int.MaxValue)));
        }
    }

    /// <summary>
    /// Typing an interface's family word (from three letters: gra → gradients, sha → shapes) or the
    /// interface's own name (IGr) lists every struct that implements it, written in whole.
    /// </summary>
    private void OfferFamilies(Caret caret, List<Offer> offers)
    {
        if (caret.Typed.Length < 3 || caret.NewName)
            return;
        foreach (string face in _interfaceNames)
        {
            if (!(FamilyWord(face) + "s").StartsWith(caret.Typed, StringComparison.OrdinalIgnoreCase)
                && !face.StartsWith(caret.Typed, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (Completion member in Family(face))
                offers.Add(new Offer(member, Tier.Family, Fit.Prefix));
        }
    }

    /// <summary>An interface's family word: IShape → shape, IGradient → gradient.</summary>
    private static string FamilyWord(string face) =>
        face.Length > 1 && face[0] == 'I' && char.IsUpper(face[1]) ? char.ToLowerInvariant(face[1]) + face[2..] : face.ToLowerInvariant();

    /// <summary>
    /// The structs that implement an interface (its interfaces' too: every IShape is an IPlaced),
    /// the library's and the code's, each written in as <c>Name($)</c>. Generic ones are left out:
    /// methods make them (repeated, mirrored, unioned), nobody writes them.
    /// </summary>
    private List<Completion> Family(string face)
    {
        if (_families.TryGetValue(face, out var known))
            return known;
        var members = _globals.Where(g => g.Kind == CompletionKind.Type && Faces(g.Name).Contains(face))
            .Select(Called).ToList();
        _families[face] = members;
        IEnumerable<Completion> own = _code.Types.Where(t => Faces(t.Name).Contains(face)).Select(Called);
        return [.. members, .. own];
    }

    private readonly Dictionary<string, List<Completion>> _families = new(StringComparer.Ordinal);

    /// <summary>A type written in as a call to make one, the caret between the brackets.</summary>
    private static Completion Called(Completion type) => type with { Text = type.Name + "($)" };

    /// <summary>The words that could complete what is typed: the code's, the host's, the library's, Slang's.</summary>
    private void OfferWords(Caret caret, IReadOnlyList<string> code, List<Offer> offers)
    {
        if (caret.Typed.Length == 0 || caret.NewName)
            return;
        void Offer(Completion completion) => offers.Add(new Offer(completion, Tier.Plain, Matching(completion.Name, caret.Typed, 3)));

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
    }

    /// <summary>After a dot: the scope's members, its interfaces' and, for a vector, its components.</summary>
    private void OfferMembers(Caret caret, List<Offer> offers)
    {
        foreach (Completion member in Members(caret.Scope!))
            offers.Add(new Offer(member, Tier.Plain, Matching(member.Name, caret.Typed, 1)));
    }

    /// <summary>
    /// How <paramref name="typed"/> matches <paramref name="name"/>: its start, the starts of its
    /// humps in order (oB → outBounce, inOQ → inOutQuad), or anywhere inside once
    /// <paramref name="insideFrom"/> letters are typed. Nothing typed matches everything.
    /// </summary>
    private static Fit Matching(string name, string typed, int insideFrom)
    {
        if (name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            return Fit.Prefix;
        if (typed.Length >= 2 && HumpsMatch(Humps(name), 0, typed, 0))
            return Fit.Humps;
        if (typed.Length >= insideFrom && name.Contains(typed, StringComparison.OrdinalIgnoreCase))
            return Fit.Inside;
        return Fit.None;
    }

    /// <summary>A name cut where a capital, a digit run or an underscore starts a new part: inOutQuad → in, Out, Quad.</summary>
    private static List<string> Humps(string name)
    {
        var humps = new List<string>();
        int from = 0;
        for (int i = 1; i <= name.Length; i++)
        {
            bool cut = i == name.Length || char.IsUpper(name[i]) || name[i] == '_'
                       || (char.IsDigit(name[i]) && !char.IsDigit(name[i - 1]));
            if (!cut)
                continue;
            string hump = name[from..i].TrimStart('_');
            if (hump.Length > 0)
                humps.Add(hump);
            from = i;
        }

        return humps;
    }

    /// <summary>Whether typed[at..] is made of starts of humps from <paramref name="hump"/> on, the first hump never skipped.</summary>
    private static bool HumpsMatch(List<string> humps, int hump, string typed, int at)
    {
        if (at == typed.Length)
            return true;
        for (int h = hump; h < humps.Count; h++)
        {
            if (hump == 0 && h > 0)
                return false;
            int longest = 0;
            while (longest < humps[h].Length && at + longest < typed.Length
                   && char.ToLowerInvariant(humps[h][longest]) == char.ToLowerInvariant(typed[at + longest]))
                longest++;
            for (int take = longest; take >= 1; take--)
            {
                if (HumpsMatch(humps, h + 1, typed, at + take))
                    return true;
            }
        }

        return false;
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

    /// <summary>
    /// The statement snippets for a line that starts with (part of) their keyword, even once the
    /// keyword is typed in full: body ones inside a function, top-level ones outside.
    /// <paramref name="start"/> is where the keyword begins.
    /// </summary>
    private static List<Completion>? StatementSnippets(string beforeCaret, IReadOnlyList<string> code, int caretLine, out int start)
    {
        start = 0;
        Match match = StatementStart().Match(beforeCaret);
        if (caretLine < 0 || caretLine >= code.Count || !match.Success)
            return null;

        bool topLevel = Depth(code, caretLine) == 0;
        string indent = match.Groups[1].Value;
        string typed = match.Groups[2].Value;
        var found = new List<Completion>();
        foreach ((string name, string header, string text, bool top) in Statements)
        {
            if (top == topLevel && name.StartsWith(typed, StringComparison.Ordinal))
                found.Add(new Completion(name, CompletionKind.Snippet, header, Text: text.Replace("\n", "\n" + indent)));
        }

        if (found.Count == 0)
            return null;
        start = indent.Length;
        return found;
    }

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
}
