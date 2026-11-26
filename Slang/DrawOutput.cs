using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One target a <c>[Draw]</c> writes, from its shade's return type (Docs/render-graph.md, stage C): a <c>float4</c> (or
/// <c>colour</c>) shade writes one, blended out where its alpha is 0; a <c>float</c>, <c>float2</c> or <c>uint</c> shade
/// writes that one value (a shadow map's depth); a shade returning a struct writes one per field, in order. Its call names an image for each, checked against the field's type.
/// </summary>
/// <param name="Field">The struct field it comes from; null for a shade that returns its colour.</param>
/// <param name="Type">What the field holds: any type an image holds (<see cref="Texel.All"/>), a colour as float4.</param>
public sealed record DrawOutput(string? Field, string Type)
{
    /// <summary>SDL_GPU's colour targets a render pass. A draw with more outputs packs narrow ones into shared targets (<see cref="DrawPacking"/>).</summary>
    public const int Most = 4;

    /// <summary>What its channels hold and how many.</summary>
    public Texel Texel => Texel.Of(Type, null)!;

    /// <summary>The image formats that hold it.</summary>
    public string[] Formats => Texel.FormatsFor(Type);

    /// <summary>What a draw's call names its image for, in messages: the shade's colour, or one of its fields.</summary>
    public string For(string shade) => Field is null ? $"{shade}'s colour" : $"{shade}'s {Field}";

    /// <summary>
    /// The outputs of the shade <paramref name="shade"/>, from its return type: declared in <paramref name="code"/> (the
    /// body, comments blanked) or in a library module. Throws <see cref="BuildProblem"/> at <paramref name="at"/> when
    /// it can't be drawn. A float field marked <c>: SV_Depth</c> isn't a target: it is the pixel's depth, pixels into
    /// the screen, written into the draw's depth (<paramref name="depth"/> names it; null without one).
    /// </summary>
    internal static DrawOutput[] Of(string code, string shade, int at, out string? depth)
    {
        depth = null;
        Match declared = Regex.Match(code, $@"\b(\w+)\s+{Regex.Escape(shade)}\s*\(");
        if (!declared.Success)
            throw new BuildProblem(at, $"the draw is shaded by {shade}; declare float4 {shade}(ItemInput i), or a struct of outputs as its return type.");
        string type = declared.Groups[1].Value;
        if (type is "float4" or "colour")
            return [new DrawOutput(null, "float4")];
        if (Texel.Of(type, null) is not null)
            return [new DrawOutput(null, type)];

        string body = StructBody(code, type)
                      ?? SlangShaders.LibraryModules().Select(module => StructBody(BuildText.Blank(module.Text), type)).FirstOrDefault(found => found is not null)
                      ?? throw new BuildProblem(declared.Index, $"{shade} returns {type}, which is neither a float4, float, float2, uint nor a struct the stack or the library declares.");
        var outputs = new List<DrawOutput>();
        foreach (string part in TopLevel(body).Split(';'))
        {
            Match field = Regex.Match(part, @"^\s*(?:public\s+)?(\w+)\s+(\w+)\s*(?<depth>:\s*SV_Depth\s*)?$");
            if (!field.Success)
                continue;
            if (field.Groups["depth"].Success)
            {
                if (field.Groups[1].Value != "float" || depth is not null)
                    throw new BuildProblem(declared.Index, $"a draw writes one depth, a float marked : SV_Depth; {type} has {field.Groups[2].Value}.");
                depth = field.Groups[2].Value;
                continue;
            }

            string held = field.Groups[1].Value == "colour" ? "float4" : field.Groups[1].Value;
            if (Texel.Of(held, null) is null)
                throw new BuildProblem(declared.Index,
                    $"a draw writes each field of {type} to an image, so each holds what an image can ({string.Join(", ", Texel.Types)} or colour); {field.Groups[2].Value} is {field.Groups[1].Value}.");
            outputs.Add(new DrawOutput(field.Groups[2].Value, held));
        }

        if (outputs.Count is 0 or > StackBuild.MaxArguments)
            throw new BuildProblem(declared.Index, $"a draw writes 1 to {StackBuild.MaxArguments} images; {type} has {outputs.Count} fields.");
        return [.. outputs];
    }

    /// <summary>The text inside <c>struct name { … }</c>, or null when <paramref name="code"/> doesn't declare it.</summary>
    private static string? StructBody(string code, string name)
    {
        Match found = Regex.Match(code, $@"\bstruct\s+{Regex.Escape(name)}\s*\{{");
        if (!found.Success)
            return null;
        int open = found.Index + found.Length - 1, close = BuildText.Matching(code, open);
        return close < 0 ? null : code[(open + 1)..close];
    }

    /// <summary>A struct's body with every method's braces cut out and a <c>;</c> left in their place, so only fields remain whole.</summary>
    private static string TopLevel(string body)
    {
        var kept = new StringBuilder();
        int depth = 0;
        foreach (char c in body)
        {
            if (c == '{' && depth++ == 0)
                kept.Append(';');
            else if (c == '}')
                depth--;
            else if (depth == 0)
                kept.Append(c);
        }

        return kept.ToString();
    }
}
