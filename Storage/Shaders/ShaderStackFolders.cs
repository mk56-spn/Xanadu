using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xanadu.Core.Storage;
using Xanadu.Model.Levels;

namespace Xanadu.Storage.Shaders;

/// <summary>
/// The shader stacks in the game folder: every folder under <c>Assets/Shaders</c>
/// holding a <c>.slang</c> file, the library left out.
/// </summary>
public sealed class ShaderStackFolders : IShaderStackFolders
{
    private const string Root = "Assets/Shaders";

    public IReadOnlyList<string> List()
    {
        string root = GamePaths.Full(Root);
        if (!Directory.Exists(root))
            return [];
        string library = Path.Combine(root, "Library");
        return
        [
            .. Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                .Where(folder => !folder.StartsWith(library, StringComparison.Ordinal))
                .Where(folder => Directory.EnumerateFiles(folder, "*.slang").Any())
                .Select(folder => Root + "/" + Path.GetRelativePath(root, folder).Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
        ];
    }
}
