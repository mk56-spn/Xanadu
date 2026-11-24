using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xanadu.Tools.DevTools.Deps;

/// <summary>One built project DLL: when it was built and the project DLLs it really loads.</summary>
internal sealed record DepsAssembly(string Name, int Level, DateTime Built, IReadOnlyList<string> Uses)
{
    public string Short => DepsGraph.Short(Name);
}

/// <summary>
/// Which project assemblies each built Xanadu DLL really loads, read from its
/// AssemblyRef table: the compiler writes only references the code uses, so a
/// csproj reference nothing touches doesn't show. Each assembly gets a level:
/// 1 for none, otherwise one above its highest reference.
/// </summary>
internal sealed class DepsGraph
{
    private const string Prefix = "Xanadu.";

    private readonly Dictionary<string, DepsAssembly> _byName = new(StringComparer.Ordinal);

    /// <summary>Every assembly, by level then name.</summary>
    public IReadOnlyList<DepsAssembly> Assemblies { get; }

    /// <summary>A cycle found while levelling, named (none should exist: MSBuild refuses them).</summary>
    public string? Cycle { get; private set; }

    private DepsGraph(Dictionary<string, (List<string> Uses, DateTime Built)> read)
    {
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();

        int Level(string name)
        {
            if (levels.TryGetValue(name, out int known))
                return known;
            if (!read.TryGetValue(name, out var node))
                return 0; // referenced but never built here
            if (path.Contains(name))
            {
                Cycle ??= string.Join(" -> ", path.SkipWhile(n => n != name).Append(name).Select(Short));
                return 0;
            }

            path.Add(name);
            int level = 1 + node.Uses.Select(Level).DefaultIfEmpty(0).Max();
            path.RemoveAt(path.Count - 1);
            return levels[name] = level;
        }

        foreach (var (name, (uses, built)) in read)
            _byName[name] = new DepsAssembly(name, Level(name), built, uses);
        Assemblies = [.. _byName.Values.OrderBy(a => a.Level).ThenBy(a => a.Name, StringComparer.Ordinal)];
    }

    public DepsAssembly? Find(string name) =>
        Assemblies.FirstOrDefault(a => a.Short.Equals(name, StringComparison.OrdinalIgnoreCase) || a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The assemblies that reference <paramref name="assembly"/> directly.</summary>
    public IEnumerable<DepsAssembly> UsedBy(DepsAssembly assembly) => Assemblies.Where(a => a.Uses.Contains(assembly.Name));

    /// <summary>What it loads only through others: every assembly it reaches, less the ones it references itself.</summary>
    public IEnumerable<DepsAssembly> Through(DepsAssembly assembly)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(assembly.Uses);
        while (stack.TryPop(out string? next))
        {
            if (reached.Add(next) && _byName.TryGetValue(next, out var node))
                foreach (string use in node.Uses)
                    stack.Push(use);
        }

        return Assemblies.Where(a => reached.Contains(a.Name) && !assembly.Uses.Contains(a.Name));
    }

    /// <summary>The newest build of each Xanadu assembly under <paramref name="root"/>, tests included.</summary>
    public static DepsGraph Read(string root)
    {
        var newest = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        string bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        foreach (string path in Directory.EnumerateFiles(root, "Xanadu*.dll", SearchOption.AllDirectories))
        {
            if (!path.Contains(bin, StringComparison.Ordinal))
                continue;
            var file = new FileInfo(path);
            if (!newest.TryGetValue(file.Name, out FileInfo? known) || file.LastWriteTimeUtc > known.LastWriteTimeUtc)
                newest[file.Name] = file;
        }

        var read = new Dictionary<string, (List<string>, DateTime)>(StringComparer.Ordinal);
        foreach (FileInfo file in newest.Values)
        {
            using var pe = new PEReader(File.OpenRead(file.FullName));
            if (!pe.HasMetadata)
                continue;
            MetadataReader meta = pe.GetMetadataReader();
            List<string> uses = [.. meta.AssemblyReferences
                .Select(handle => meta.GetString(meta.GetAssemblyReference(handle).Name))
                .Where(reference => reference.StartsWith("Xanadu", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)];
            read[meta.GetString(meta.GetAssemblyDefinition().Name)] = (uses, file.LastWriteTime);
        }

        return new DepsGraph(read);
    }

    public static string Short(string name) => name.StartsWith(Prefix, StringComparison.Ordinal) ? name[Prefix.Length..] : name;

    public static string List(IEnumerable<string> names) =>
        names.Any() ? string.Join(", ", names.Select(Short).Order(StringComparer.Ordinal)) : "-";

    /// <summary>How long ago it was built, so a stale DLL stands out.</summary>
    public static string Age(DateTime built) => (DateTime.Now - built) switch
    {
        { TotalMinutes: < 60 } span => $"{(int)span.TotalMinutes}m",
        { TotalHours: < 48 } span => $"{(int)span.TotalHours}h",
        var span => $"{(int)span.TotalDays}d",
    };
}
