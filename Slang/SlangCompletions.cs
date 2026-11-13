using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

public enum CompletionKind
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
public readonly record struct Completion(string Name, CompletionKind Kind, string Detail, Vector4? Swatch = null, string? Doc = null, string? Text = null)
{
    /// <summary>Where a snippet's <see cref="Text"/> puts the caret.</summary>
    public const char Caret = '$';

    /// <summary>How hovering names it: a call with its signature, a value with its type first.</summary>
    public string Header => Kind switch
    {
        CompletionKind.Function or CompletionKind.Method => Name + Detail,
        // A struct's constructor makes one of it.
        CompletionKind.Type when Detail.StartsWith('(') => Name + Detail + " → " + Name,
        CompletionKind.Type => Detail + " " + Name,
        CompletionKind.EnumValue => Detail + "." + Name,
        CompletionKind.Namespace => "namespace " + Name,
        CompletionKind.Snippet => Detail,
        _ => Detail + " " + Name,
    };
}

/// <summary>
/// Completion for the shader editor, built from the shader library's own source
/// and a light scan of the code being edited. Three layers (the rules are in
/// Docs/shaders.md, "Completion"):
/// <list type="number">
/// <item><b>Model</b> (<c>.Library</c>, <c>.Code</c>): what the library and the code declare,
/// which structs implement which interface, what each call's parameters are.</item>
/// <item><b>Caret</b> (<c>.Caret</c>): where the caret is, worked out once: after a dot, typing a
/// word, giving a new name, at the start of a line, and where a value starts, what type it wants.</item>
/// <item><b>Rules</b> (<c>.Rules</c>): each offers candidates at a <see cref="Tier"/>; one ranking
/// here orders, merges and trims them. A new behaviour is a rule or a tag, never a branch here.</item>
/// </list>
/// </summary>
public sealed partial class SlangCompletions
{
    /// <param name="provided">Names the host declares for the code, with their types.</param>
    public static SlangCompletions FromSources(
        IEnumerable<string> sources, ShaderConstants constants, IEnumerable<(string Name, string Type, string? Doc)>? provided = null)
    {
        var completions = new SlangCompletions();
        foreach (string source in sources)
            completions.Read(source, constants);
        completions.InheritInterfaces();
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
        Learn(code);
        Caret caret = Locate(beforeCaret, code, caretLine);
        start = caret.Start;
        if (caret.Where == Where.Nowhere)
            return null;

        var offers = new List<Offer>();
        if (caret.Where == Where.Member)
            OfferMembers(caret, offers);
        else if (OfferSnippets(caret, code, offers, out int lineStart))
            start = lineStart;
        else
        {
            OfferExpected(caret, offers);
            OfferFamilies(caret, offers);
            OfferWords(caret, code, offers);
        }

        return Ranked(caret, offers);
    }

    /// <summary>How strongly a rule offers something; lower comes first, whatever the match.</summary>
    private enum Tier
    {
        /// <summary>A whole statement or function for the line being started.</summary>
        Snippet,

        /// <summary>Makes the type the caret's value wants (a named colour for a colour, a shape for an IShape).</summary>
        Expected,

        /// <summary>A member of the family the typed word names (grad → the gradients).</summary>
        Family,

        /// <summary>Anything else whose name matches.</summary>
        Plain,
    }

    /// <summary>How well the typed text matches a name; lower comes first within a tier.</summary>
    private enum Fit
    {
        Prefix,
        /// <summary>The typed letters start the name's humps in order (oB → outBounce).</summary>
        Humps,
        /// <summary>Somewhere inside the name, from three letters on.</summary>
        Inside,
        None,
    }

    /// <summary>One candidate a rule offers, with how strongly.</summary>
    private readonly record struct Offer(Completion Completion, Tier Tier, Fit Fit);

    /// <summary>
    /// The one ordering: by tier, then fit, then the code's and the host's names before the
    /// library's, then alphabetically. A name offered twice keeps its best place (snippets may share
    /// a name). Nothing is offered once the typed word is complete as written (Enter must stay a
    /// new line), except snippets, which add more than the word.
    /// </summary>
    private static List<Completion>? Ranked(Caret caret, List<Offer> offers)
    {
        if (caret.Typed.Length > 0 && offers.Exists(o => o.Completion.Kind != CompletionKind.Snippet && Complete(o.Completion, caret.Typed)))
            offers.RemoveAll(o => o.Completion.Kind != CompletionKind.Snippet);

        var found = new List<Completion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var snippets = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<Offer> ordered = offers
            .Where(o => o.Fit != Fit.None)
            .OrderBy(o => o.Tier)
            .ThenBy(o => o.Fit)
            .ThenBy(o => o.Completion.Kind == CompletionKind.Variable ? 0 : 1)
            .ThenBy(o => o.Completion.Name, StringComparer.OrdinalIgnoreCase);
        foreach (Offer offer in ordered)
        {
            Completion completion = offer.Completion;
            bool snippet = completion.Kind == CompletionKind.Snippet;
            if (snippet ? snippets.Add(completion.Name + "\0" + completion.Text) : !seen.Contains(completion.Name))
                found.Add(completion);
            seen.Add(completion.Name);
        }

        return found.Count == 0 ? null : found;
    }

    /// <summary>Whether taking the completion would leave the typed word as it is (or only add its brackets).</summary>
    private static bool Complete(Completion completion, string typed) =>
        completion.Name == typed && (completion.Text is null || completion.Text.StartsWith(typed + "(", StringComparison.Ordinal));
}
