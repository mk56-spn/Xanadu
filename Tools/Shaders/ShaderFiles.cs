using System;
using System.IO;
using System.Linq;
using Xanadu.Storage;

namespace Xanadu.Tools.Shaders;

/// <summary>One shipped effect: its <c>res://</c> path (how materials name it) and a short name for the list.</summary>
internal sealed record ShaderFile(string Path, string Name);

/// <summary>
/// The shader tool's only door to the disk: which effects ship (every
/// <c>.slang</c> under <c>Assets/Shaders</c> outside the library, as the
/// renderer warms them), their text, and saving it.
/// </summary>
internal static class ShaderFiles
{
    private const string Root = "res://Assets/Shaders";

    public static ShaderFile[] All()
    {
        string root = GamePaths.Globalize(Root);
        string library = System.IO.Path.Combine(root, "Library");
        return Directory.EnumerateFiles(root, "*.slang", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(library, StringComparison.Ordinal))
            .Select(file => System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .Select(relative => new ShaderFile($"{Root}/{relative}", relative[..^".slang".Length]))
            .ToArray();
    }

    public static string Read(ShaderFile file) => File.ReadAllText(GamePaths.Globalize(file.Path));

    /// <summary>Writes the text over the file; false (and the message) when the disk refuses.</summary>
    public static bool Write(ShaderFile file, string text, out string? error)
    {
        try
        {
            File.WriteAllText(GamePaths.Globalize(file.Path), text);
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
