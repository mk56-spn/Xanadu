using System.Collections.Generic;
using Xanadu.Rendering;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// One value a material's shader reads, as the host's compiler reports it:
/// its name, how many channels, whether it is an integer, and its default.
/// </summary>
public readonly record struct MaterialParam(string Name, int Channels, bool IsInt, Vector4 Default);

/// <summary>
/// What a canvas item draws with when it isn't the plain canvas shader: a
/// shader (a Slang effect by its path in the game folder, Docs/host.md §5.5),
/// a blend mode, and named values and textures the shader reads. Engine-free,
/// so scenes build and tune materials without Godot; the host compiles the
/// shader and packs the values by name (fields of its <c>Params</c> block).
/// </summary>
/// <remarks>
/// Values are kept as <see cref="Vector4"/>; a float uses X, a colour XYZW as
/// RGBA. A value the material doesn't set keeps the shader's own default.
/// </remarks>
public sealed class CanvasMaterial(string shader, BlendMode blend = BlendMode.Mix, string? instance = null)
{
    private readonly Dictionary<string, Vector4> _values = new();
    private readonly Dictionary<string, Handle> _textures = new();

    /// <summary>
    /// The effect's path, such as <c>Assets/Shaders/note_background.slang</c>;
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
    /// <param name="keepsState">Lets the code keep state with <c>update</c> (Docs/shaders.md); the lab only, for now.</param>
    public static CanvasMaterial FromSource(string name, string source, BlendMode blend = BlendMode.Mix, bool keepsState = false) =>
        new(name, blend) { Source = source, KeepsState = keepsState };

    /// <summary>
    /// A material for a whole effect's text (its own <c>shade</c> entry point, as
    /// a shipped <c>.slang</c> file has), for one being edited before it is saved.
    /// </summary>
    public static CanvasMaterial FromEffect(string name, string source, BlendMode blend = BlendMode.Mix) =>
        new(name, blend) { Source = source, Whole = true };

    /// <summary>True when <see cref="Source"/> is a whole effect, not an effect body to wrap.</summary>
    public bool Whole { get; private init; }

    /// <summary>
    /// Whether the code may keep state between frames (an <c>update</c> and its
    /// <c>State</c>). Each material keeps its own, starting zeroed.
    /// </summary>
    public bool KeepsState { get; private init; }

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

    /// <summary>The value set under <paramref name="name"/>, or <paramref name="fallback"/>.</summary>
    public Vector4 Get(string name, Vector4 fallback = default) =>
        _values.TryGetValue(name, out Vector4 value) ? value : fallback;

    public float GetFloat(string name, float fallback = 0f) =>
        _values.TryGetValue(name, out Vector4 value) ? value.X : fallback;
}
