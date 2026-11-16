using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// The shader library precompiled to <c>.slang-module</c> files, so a compile
/// loads the library ready-checked instead of parsing and checking every module
/// again: most of a fresh compile's time went there. Built once per library
/// version and set of defines into the shader cache, then put first on the
/// include path; Slang takes a <c>.slang-module</c> over the source beside it.
/// <para>
/// Each built module is also kept in a store by what it was built from (its text, what it
/// imports, and the defines when its text reads one), and a build takes from there what it
/// can. So an edit builds only the edited module and those that import it, and another set
/// of defines (the chart's binding moves with an effect's textures) only <c>xanadu.stage</c>,
/// which reads them: the modules that import it are the same bytes either way.
/// </para>
/// </summary>
/// <remarks>
/// Slang never asks whether a module is stale, so the folder is named by a hash of
/// every library file (and the defines baked into <c>xanadu.stage</c>): any edit
/// builds a fresh set. Anything that fails leaves the caller on the plain source.
/// <para>
/// Slang (2026.21–22) can't read back a stored vector or matrix made from several
/// numbers (<c>static const float4 gold = float4(1, 0.84, 0, 1)</c>): an importer
/// that uses one dies with "Generic type/value shouldn't be handled here!". So the
/// copy built from has each such constant as a getter, on the same line so
/// diagnostics keep their places; the library itself stays as written. A mutable
/// global made that way still fails, so the library doesn't declare any.
/// </para>
/// </remarks>
public static partial class SlangModules
{
    [GeneratedRegex(@"public\s+static\s+const\s+((?:colour|(?:float|int|uint|half|bool)[234](?:x[234])?))\s+(\w+)\s*=\s*([^;]+);")]
    private static partial Regex VectorConstant();

    [GeneratedRegex(@"^\s*(?:__exported\s+)?import\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex Import();

    /// <summary>A stored vector constant as a getter, in the same place on the same lines.</summary>
    public static string AsGetters(string source) =>
        VectorConstant().Replace(source, match =>
            $"public property {match.Groups[1].Value} {match.Groups[2].Value} {{ get {{ return {match.Groups[3].Value}; }} }}");

    /// <summary>
    /// Builds every module under <paramref name="library"/> into <paramref name="folder"/>, each
    /// after the ones it imports; null and the reason when Slang refuses one.
    /// </summary>
    /// <param name="store">Where built modules are kept and taken from between builds; null builds every one.</param>
    /// <param name="salt">What else a stored module depends on: the compiler's version.</param>
    public static string? Build(string library, string folder, string[] defines, out string? problem, string? store = null, string salt = "")
    {
        problem = null;
        string staging = folder + ".building";
        try
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            var modules = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(library, "*.slang", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(library, file);
                string name = Path.ChangeExtension(relative, null).Replace(Path.DirectorySeparatorChar, '.');
                string text = AsGetters(File.ReadAllText(file));
                string copy = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.WriteAllText(copy, text);
                modules[name] = text;
            }

            string[] sorted = defines.Order(StringComparer.Ordinal).ToArray();
            string[] defined = sorted.Select(define => define.Split('=')[0]).ToArray();
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in InOrder(modules))
            {
                string source = Path.Combine(staging, name.Replace('.', Path.DirectorySeparatorChar) + ".slang");
                string target = Path.ChangeExtension(source, ".slang-module");
                // What the module means: its text and everything it imports, by their own keys.
                string text = modules[name];
                keys[name] = Hash($"{salt}\n{name}\n{string.Join('\n', Imports(modules, name).Select(import => keys.GetValueOrDefault(import, "")))}\n{text}");
                // The defines only reach a module that reads one itself: each module is preprocessed on its own.
                string built = defined.Any(text.Contains) ? Hash($"{keys[name]}\n{string.Join(';', sorted)}") : keys[name];
                string? stored = store is null ? null : Path.Combine(store, $"{name}-{built}.slang-module");
                if (stored is not null && File.Exists(stored))
                {
                    File.Copy(stored, target, overwrite: true);
                    continue;
                }

                var args = new List<string> { source, "-I", staging };
                foreach (string define in defines)
                {
                    args.Add("-D");
                    args.Add(define);
                }

                if (SlangNative.BuildModule(args, out string diagnostics) is not { } module)
                {
                    problem = $"{name}: {FirstError(diagnostics)}";
                    return null;
                }

                File.WriteAllBytes(target, module);
                if (stored is not null)
                {
                    // Written whole under another name first: two launches may build the same module at once.
                    Directory.CreateDirectory(store!);
                    string writing = stored + "." + Guid.NewGuid().ToString("N");
                    File.WriteAllBytes(writing, module);
                    File.Move(writing, stored, overwrite: true);
                }
            }

            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            Directory.Move(staging, folder);
            return folder;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            problem = exception.Message;
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string Hash(string text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..24];

    /// <summary>The library modules one imports, in name order.</summary>
    private static IEnumerable<string> Imports(Dictionary<string, string> modules, string name) =>
        Import().Matches(modules[name]).Select(import => import.Groups[1].Value)
            .Where(modules.ContainsKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    /// <summary>The modules with each after everything it imports from the library.</summary>
    private static List<string> InOrder(Dictionary<string, string> modules)
    {
        var order = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        void Place(string name)
        {
            if (placed.Contains(name) || !visiting.Add(name))
                return;
            foreach (string import in Imports(modules, name))
                Place(import);

            visiting.Remove(name);
            placed.Add(name);
            order.Add(name);
        }

        foreach (string name in modules.Keys.Order(StringComparer.Ordinal))
            Place(name);
        return order;
    }

    private static string FirstError(string diagnostics)
    {
        string[] lines = diagnostics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.FirstOrDefault(line => line.Contains("error", StringComparison.Ordinal)) ?? lines.FirstOrDefault() ?? "Slang refused it";
    }
}
