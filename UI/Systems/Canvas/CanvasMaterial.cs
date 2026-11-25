using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xanadu.Core.Rendering;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// One value a material's shader reads, as the host's compiler reports it:
/// its name, how many channels (a matrix's columns), whether it is an integer,
/// and its default. A matrix has more than one of <paramref name="Rows"/> and
/// is kept with <see cref="CanvasMaterial.Set(string, Matrix4x4)"/>.
/// </summary>
public readonly record struct MaterialParam(
    string Name, int Channels, bool IsInt, Vector4 Default, bool IsBool = false, bool IsUnsigned = false, int Rows = 1,
    bool Fit = false, IReadOnlyList<string>? Options = null)
{
    public bool IsMatrix => Rows > 1;
}

/// <summary>What a material draws in place of its effect's shade (<see cref="CanvasMaterial.Overlay"/>).</summary>
public enum MaterialOverlay
{
    /// <summary>The effect itself.</summary>
    None,

    /// <summary>Every triangle's edges, line-filled after the vertex stage.</summary>
    Triangles,

    /// <summary>Lines where uv is 0 and 1 on both axes, from the uv the vertex stage hands on.</summary>
    UvLines,
}

/// <summary>
/// What a canvas item draws with when it isn't the plain canvas shader: a
/// shader (a Slang effect by its path in the game folder, Docs/host.md §5.5),
/// a blend mode, and named values and textures the shader reads. Engine-free,
/// so scenes build and tune materials without Godot; the host compiles the
/// shader and packs the values by name (fields of its <c>Params</c> block).
/// </summary>
/// <remarks>
/// Values are kept as <see cref="Vector4"/>; a float uses X, a colour XYZW as
/// RGBA. Matrices are kept apart, as <see cref="Matrix4x4"/> read by row and
/// column (<c>m[row, column]</c>), the unused corner ignored. A value the
/// material doesn't set keeps the shader's own default; an unset matrix is
/// the identity.
/// </remarks>
public sealed class CanvasMaterial(string shader, BlendMode blend = BlendMode.Mix, string? instance = null)
{
    /// <summary>
    /// The run value the host hands every shader as <c>unit</c> (screen pixels per item unit), not a uniform: its name
    /// can't be one, so no shader's value meets it.
    /// </summary>
    public const string Unit = "xanadu.unit";

    private readonly Dictionary<string, Vector4> _values = [];
    private readonly Dictionary<string, Handle> _textures = [];
    private readonly Dictionary<string, Matrix4x4> _matrices = [];
    private readonly Dictionary<string, byte[]> _buffers = [];

    /// <summary>
    /// The effect's path, such as <c>Assets/Shaders/Game/note_background</c>;
    /// empty for the plain canvas shader. For an effect made from <see cref="Source"/>,
    /// the name it is cached and reported by.
    /// </summary>
    public string Shader { get; } = shader;

    /// <summary>
    /// A level shader's own code (an effect body: uniforms and
    /// <c>float4 shade(Canvas canvas)</c>), which the host wraps into an entry
    /// point; null for a shipped effect at <see cref="Shader"/>.
    /// </summary>
    public string? Source { get; private init; }

    /// <summary>A material for a level shader's code; <paramref name="name"/> names it in errors and caches.</summary>
    /// <param name="overlay">Draws one of the shader editor's overlays instead of the code's shade (see <see cref="Overlay"/>).</param>
    public static CanvasMaterial FromSource(
        string name, string source, BlendMode blend = BlendMode.Mix, MaterialOverlay overlay = MaterialOverlay.None) =>
        new(name, blend) { Source = source, Overlay = overlay };

    /// <summary>
    /// Draws a look of the shader editor's instead of the effect's own shade, through
    /// the effect's own vertex stage (its <c>deform</c>), so it shows where the
    /// triangles and the uv really land; values still feed the vertex stage.
    /// </summary>
    public MaterialOverlay Overlay { get; private init; }

    public BlendMode Blend { get; } = blend;

    /// <summary>
    /// The full fixed-function blend, overriding <see cref="Blend"/> when set
    /// (Labs' blend lab). Fixed for the material's life: make a new material to change it.
    /// </summary>
    public CustomBlend? Custom { get; init; }

    /// <summary>A name for this use of the shader (such as "background"), so the tuner can tell uses apart.</summary>
    public string? Instance { get; } = instance;

    /// <summary>Goes up whenever a value or texture changes.</summary>
    public int Version { get; private set; }

    public IReadOnlyDictionary<string, Vector4> Values => _values;

    /// <summary>The matrix values by name, read <c>m[row, column]</c>.</summary>
    public IReadOnlyDictionary<string, Matrix4x4> Matrices => _matrices;

    public void Set(string name, Matrix4x4 value)
    {
        if (_matrices.TryGetValue(name, out Matrix4x4 old) && old == value)
            return;

        _matrices[name] = value;
        Version++;
    }

    /// <summary>Textures by the name of the sampler that reads them; ids from the canvas server.</summary>
    public IReadOnlyDictionary<string, Handle> Textures => _textures;

    public void Set(string name, Vector4 value)
    {
        if (_values.TryGetValue(name, out Vector4 old) && old == value)
            return;

        _values[name] = value;
        Version++;
    }

    public void Set(string name, float value) => Set(name, new Vector4(value, 0, 0, 0));

    public void Set(string name, int value) => Set(name, new Vector4(value, 0, 0, 0));

    public void Set(string name, Vector2 value) => Set(name, new Vector4(value.X, value.Y, 0, 0));

    public void Set(string name, Vector3 value) => Set(name, new Vector4(value.X, value.Y, value.Z, 0));

    public void Set(string name, Color value) => Set(name, new Vector4(value.R, value.G, value.B, value.A));

    public void SetTexture(string name, Handle texture)
    {
        if (_textures.TryGetValue(name, out Handle old) && old == texture)
            return;

        _textures[name] = texture;
        Version++;
    }

    /// <summary>
    /// Data for the stack's <c>[Host(size)] RWStructuredBuffer&lt;T&gt; name;</c>, by name, as bytes laid out as the
    /// shader's <c>T</c> (std430). The host uploads them when <see cref="BufferVersion"/> moves; past the buffer's
    /// size is cut off, and what isn't covered keeps what was there.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Buffers => _buffers;

    /// <summary>Goes up whenever a buffer's data is set.</summary>
    public int BufferVersion { get; private set; }

    /// <summary>Sets a host buffer's data (see <see cref="Buffers"/>); data the same as last time changes nothing.</summary>
    public void SetBuffer<T>(string name, ReadOnlySpan<T> data) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        if (_buffers.TryGetValue(name, out byte[]? old) && bytes.SequenceEqual(old))
            return;

        _buffers[name] = bytes.ToArray();
        BufferVersion++;
    }

    /// <summary>The value set under <paramref name="name"/>, or <paramref name="fallback"/>.</summary>
    public Vector4 Get(string name, Vector4 fallback = default) =>
        _values.TryGetValue(name, out Vector4 value) ? value : fallback;

    public float GetFloat(string name, float fallback = 0f) =>
        _values.TryGetValue(name, out Vector4 value) ? value.X : fallback;
}
