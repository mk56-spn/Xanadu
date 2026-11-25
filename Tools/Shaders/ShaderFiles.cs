using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xanadu.Core.Storage;
using Xanadu.Slang;

namespace Xanadu.Tools.Shaders;

/// <summary>One shipped shader stack: its folder in the game folder (how materials name it) and a short name for the list.</summary>
internal sealed record ShaderFile(string Path, string Name);

/// <summary>
/// The shader tool's only door to the disk: which stacks ship (every
/// <see cref="ShaderStack"/> folder under <c>Assets/Shaders</c> outside the
/// library, as the renderer warms them), their files, saving them, and making new ones.
/// </summary>
internal static class ShaderFiles
{
    private const string Root = "Assets/Shaders";

    /// <summary>The effect drawn behind every preview, so ones that read what is behind them (frosted glass) have something to show.</summary>
    public const string Backdrop = Root + "/Misc/ShaderBackground";

    public static ShaderFile[] All()
    {
        string root = GamePaths.Full(Root);
        return [.. ShaderStack.Under(root, skip: System.IO.Path.Combine(root, "Library"))
            .Select(folder => System.IO.Path.GetRelativePath(root, folder).Replace('\\', '/'))
            .Select(relative => new ShaderFile($"{Root}/{relative}", relative))];
    }

    /// <summary>
    /// Makes a new stack, one shade file (<see cref="ShaderStack.NewStack"/>), at the top of <c>Assets/Shaders</c>,
    /// named <c>new_shader</c> (then <c>new_shader_2</c>, …) so it never overwrites one; false (and
    /// the message) when the disk refuses. Rename the folder to give it a real name.
    /// </summary>
    public static bool Create(out ShaderFile? file, out string? error)
    {
        string name = "new_shader";
        for (int n = 2; Directory.Exists(GamePaths.Full($"{Root}/{name}")); n++)
            name = $"new_shader_{n}";

        file = new ShaderFile($"{Root}/{name}", name);
        if (Write(file, [ShaderStack.NewStack()], out error))
            return true;
        file = null;
        return false;
    }

    public static List<StackFile> Read(ShaderFile file) => ShaderStack.Read(GamePaths.Full(file.Path));

    /// <summary>Writes the files into the stack's folder, deleting the ones it no longer has; false (and the message) when the disk refuses.</summary>
    public static bool Write(ShaderFile file, IReadOnlyList<StackFile> files, out string? error)
    {
        try
        {
            ShaderStack.Write(GamePaths.Full(file.Path), files);
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
