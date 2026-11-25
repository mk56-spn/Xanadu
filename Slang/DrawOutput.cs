using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One target a <c>[Draw]</c> writes, from its shade's return type (Docs/render-graph.md, stage C): a <c>float4</c> (or
/// <c>colour</c>) shade writes one, blended out where its alpha is 0; a shade returning a struct writes one per field,
/// in order. Its call names an image for each, checked against the field's type.
/// </summary>
/// <param name="Field">The struct field it comes from; null for a shade that returns its colour.</param>
/// <param name="Type">What the field holds: float, float2, float4 (a colour too) or uint.</param>
public sealed record DrawOutput(string? Field, string Type)
{
    /// <summary>SDL_GPU's colour targets a render pass.</summary>
    public const int Most = 4;

    /// <summary>The image formats that hold it.</summary>
    public string[] Formats => Type switch
    {
        "float" => ["r32f", "r16f"],
        "float2" => ["rg16f"],
        "uint" => ["r32ui"],
        _ => ["rgba16f", "rgba8", "rgba32f"],
    };

    /// <summary>What a draw's call names its image for, in messages: the shade's colour, or one of its fields.</summary>
    public string For(string shade) => Field is null ? $"{shade}'s colour" : $"{shade}'s {Field}";

    /// <summary>
    /// The outputs of the shade <paramref name="shade"/>, from its return type: declared in <paramref name="code"/> (the
    /// body, comments blanked) or in a library module. Throws <see cref="BuildProblem"/> at <paramref name="at"/> when
    /// it can't be drawn.
    /// </summary>
    internal static DrawOutput[] Of(string code, string shade, int at)
    {
        Match declared = Regex.Match(code, $@"\b(\w+)\s+{Regex.Escape(shade)}\s*\(");
        if (!declared.Success)
            throw new BuildProblem(at, $"the draw is shaded by {shade}; declare float4 {shade}(ItemInput i), or a struct of outputs as its return type.");
        string type = declared.Groups[1].Value;
        if (type is "float4" or "colour")
            return [new DrawOutput(null, "float4")];

        string body = StructBody(code, type)
                      ?? SlangShaders.LibraryModules().Select(module => StructBody(BuildText.Blank(module.Text), type)).FirstOrDefault(found => found is not null)
                      ?? throw new BuildProblem(declared.Index, $"{shade} returns {type}, which is neither a float4 nor a struct the stack or the library declares.");
        var outputs = new List<DrawOutput>();
        foreach (string part in TopLevel(body).Split(';'))
        {
            Match field = Regex.Match(part, @"^\s*(?:public\s+)?(\w+)\s+(\w+)\s*$");
            if (!field.Success)
                continue;
            string held = field.Groups[1].Value == "colour" ? "float4" : field.Groups[1].Value;
            if (held is not ("float" or "float2" or "float4" or "uint"))
                throw new BuildProblem(declared.Index,
                    $"a draw writes each field of {type} to an image of its own, so each is a float, float2, float4, colour or uint; {field.Groups[2].Value} is {field.Groups[1].Value}.");
            outputs.Add(new DrawOutput(field.Groups[2].Value, held));
        }

        if (outputs.Count is 0 or > Most)
            throw new BuildProblem(declared.Index, $"a draw writes 1 to {Most} images; {type} has {outputs.Count} fields.");
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
