using System;
using System.IO;
using System.Linq;
using Xanadu.Storage;

namespace Xanadu.Tools.Shaders;

/// <summary>One shipped effect: its path in the game folder (how materials name it) and a short name for the list.</summary>
internal sealed record ShaderFile(string Path, string Name);

/// <summary>
/// The shader tool's only door to the disk: which effects ship (every
/// <c>.slang</c> under <c>Assets/Shaders</c> outside the library, as the
/// renderer warms them), their text, saving it, and making new ones.
/// </summary>
internal static class ShaderFiles
{
    private const string Root = "Assets/Shaders";

    /// <summary>The effect drawn behind every preview, so ones that read what is behind them (frosted glass) have something to show.</summary>
    public const string Backdrop = Root + "/Misc/ShaderBackground.slang";

    public static ShaderFile[] All()
    {
        string root = GamePaths.Full(Root);
        string library = System.IO.Path.Combine(root, "Library");
        return Directory.EnumerateFiles(root, "*.slang", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(library, StringComparison.Ordinal))
            .Select(file => System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .Select(relative => new ShaderFile($"{Root}/{relative}", relative[..^".slang".Length]))
            .ToArray();
    }

    /// <summary>What NEW starts from: a level effect with one tunable colour.</summary>
    private const string Template = """
        import xanadu;

        [Default(colours.gold)] uniform colour tint;

        float4 shade(ItemInput i, Canvas canvas)
        {
            colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
            let blob = Circle(120 + 20 * sin(time * 2));
            return lerp(sky, tint, blob.filled(canvas.centred, 2));
        }

        """;

    /// <summary>
    /// Writes a new effect from the template at the top of <c>Assets/Shaders</c>, named
    /// <c>new_shader</c> (then <c>new_shader_2</c>, …) so it never overwrites one; false (and
    /// the message) when the disk refuses. Rename the file to give it a real name.
    /// </summary>
    public static bool Create(out ShaderFile? file, out string? error)
    {
        string name = "new_shader";
        for (int n = 2; File.Exists(GamePaths.Full($"{Root}/{name}.slang")); n++)
            name = $"new_shader_{n}";

        file = new ShaderFile($"{Root}/{name}.slang", name);
        try
        {
            using (var stream = new FileStream(GamePaths.Full(file.Path), FileMode.CreateNew))
            using (var writer = new StreamWriter(stream))
                writer.Write(Template);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            file = null;
            error = exception.Message;
            return false;
        }
    }

    public static string Read(ShaderFile file) => File.ReadAllText(GamePaths.Full(file.Path));

    /// <summary>Writes the text over the file; false (and the message) when the disk refuses.</summary>
    public static bool Write(ShaderFile file, string text, out string? error)
    {
        try
        {
            File.WriteAllText(GamePaths.Full(file.Path), text);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }
}
