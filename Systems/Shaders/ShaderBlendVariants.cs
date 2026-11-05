using System.Text.RegularExpressions;
using Xanadu.Rendering;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// Builds a blend-mode variant of a shader's source by injecting
/// <c>render_mode blend_…</c> (docs/render-architecture.md D17, confirmed in
/// step 0, panel G). Variants are built only when a (shader, blend) pair is
/// first drawn.
/// </summary>
public static partial class ShaderBlendVariants
{
    [GeneratedRegex(@"render_mode\s+([^;]*);")]
    private static partial Regex RenderMode();

    [GeneratedRegex(@"shader_type\s+canvas_item\s*;")]
    private static partial Regex ShaderType();

    /// <summary>The Godot render_mode token a blend means, or "" for mix (the default).</summary>
    public static string Token(BlendMode blend) => blend switch
    {
        BlendMode.Add => "blend_add",
        BlendMode.Sub => "blend_sub",
        BlendMode.Mul => "blend_mul",
        BlendMode.Premultiplied => "blend_premul_alpha",
        _ => "",
    };

    /// <summary>
    /// The source with the blend injected. Mix returns the source untouched,
    /// since it is Godot's default. An existing render_mode statement keeps its
    /// other modes (unshaded, …) and swaps only its blend token; without one,
    /// the blend line goes straight after <c>shader_type canvas_item;</c>.
    /// </summary>
    public static string Inject(string source, BlendMode blend)
    {
        string token = Token(blend);
        if (token.Length == 0 || source.Length == 0)
            return source;

        Match mode = RenderMode().Match(source);
        if (mode.Success)
        {
            var tokens = new System.Collections.Generic.List<string>();
            foreach (string part in mode.Groups[1].Value.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("blend_"))
                    continue;
                tokens.Add(trimmed);
            }

            tokens.Add(token);
            string merged = "render_mode " + string.Join(", ", tokens) + ";";
            return source.Remove(mode.Index, mode.Length).Insert(mode.Index, merged);
        }

        Match header = ShaderType().Match(source);
        if (!header.Success)
            return source;

        int end = header.Index + header.Length;
        return source.Insert(end, "\nrender_mode " + token + ";");
    }
}
