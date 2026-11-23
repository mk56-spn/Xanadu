using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xanadu.Core.Rendering;

/// <summary>
/// One global every shader can read, declared by whoever owns its data
/// (Docs/shader-pipeline.md §0). The shader side is a library module's
/// <c>[Global] public StructuredBuffer&lt;T&gt; name;</c>; this is its C# side:
/// the bytes the renderer uploads and binds under that name. One writer.
/// </summary>
public sealed class ShaderGlobal
{
    private byte[] _data = [];

    internal ShaderGlobal(string name, int stride, Action<ShaderGlobal>? everyFrame)
    {
        Name = name;
        Stride = stride;
        EveryFrame = everyFrame;
    }

    /// <summary>Its name, as the shader declares it.</summary>
    public string Name { get; }

    /// <summary>Bytes in one element.</summary>
    public int Stride { get; }

    /// <summary>Elements written.</summary>
    public int Count { get; private set; }

    /// <summary>Moves on every write, so the renderer knows to upload.</summary>
    public int Version { get; private set; }

    /// <summary>Run once a frame before the upload: a writer that publishes what it reads elsewhere.</summary>
    internal Action<ShaderGlobal>? EveryFrame { get; }

    /// <summary>What was written, <see cref="Count"/> × <see cref="Stride"/> bytes.</summary>
    public ReadOnlySpan<byte> Bytes => _data.AsSpan(0, Count * Stride);

    /// <summary>Replaces its elements.</summary>
    public void Write<T>(ReadOnlySpan<T> elements) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(elements);
        if (bytes.Length % Stride != 0)
            throw new ArgumentException($"{Name}: {bytes.Length} bytes isn't a whole number of {Stride}-byte elements.");
        if (_data.Length < bytes.Length)
            _data = new byte[Math.Max(bytes.Length, _data.Length * 2)];
        bytes.CopyTo(_data);
        Count = bytes.Length / Stride;
        Version++;
    }

    /// <summary>Replaces its elements with one.</summary>
    public void Write<T>(in T element) where T : unmanaged => Write(new ReadOnlySpan<T>(in element));
}

/// <summary>
/// The globals declared in this run. The shader API knows nothing about what
/// they hold: the game declares its own (the chart, the play state), a tool
/// declares stand-ins under the same names. An effect that reads a global
/// nothing here declares doesn't compile.
/// </summary>
public static class ShaderGlobals
{
    private static readonly Dictionary<string, ShaderGlobal> Declared = new(StringComparer.Ordinal);

    /// <summary>
    /// Declares a global; declaring it again returns the one already there (its stride must match).
    /// <paramref name="everyFrame"/>, when given, writes it once a frame before the upload.
    /// </summary>
    public static ShaderGlobal Declare(string name, int stride, Action<ShaderGlobal>? everyFrame = null)
    {
        lock (Declared)
        {
            if (Declared.TryGetValue(name, out ShaderGlobal? known))
            {
                if (known.Stride != stride)
                    throw new InvalidOperationException($"Shader global {name} is already declared with {known.Stride}-byte elements, not {stride}.");
                return known;
            }

            var global = new ShaderGlobal(name, stride, everyFrame);
            Declared[name] = global;
            return global;
        }
    }

    public static bool IsDeclared(string name)
    {
        lock (Declared)
            return Declared.ContainsKey(name);
    }

    /// <summary>The global declared under <paramref name="name"/>; null when none is.</summary>
    public static ShaderGlobal? Find(string name)
    {
        lock (Declared)
            return Declared.GetValueOrDefault(name);
    }

    /// <summary>Runs every per-frame writer: the renderer calls it once a frame, before it uploads.</summary>
    public static void RunEveryFrame()
    {
        ShaderGlobal[] all;
        lock (Declared)
            all = [.. Declared.Values];
        foreach (ShaderGlobal global in all)
            global.EveryFrame?.Invoke(global);
    }
}
