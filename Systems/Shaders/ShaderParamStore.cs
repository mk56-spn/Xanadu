using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs.Storage;

namespace Xanadu.Systems.Shaders;

/// <summary>
/// Tuned shader uniform values, keyed by shader resource path and uniform
/// name, persisted as JSON so they survive restarts.
/// </summary>
public sealed class ShaderParamStore
{
    public const string UserFile = "user://shader_params.json";

    private static ShaderParamStore? _default;

    private readonly Dictionary<string, Dictionary<string, float[]>> _values;

    public ShaderParamStore(string path)
    {
        Path = path;
        try
        {
            _values = System.IO.File.Exists(path)
                ? JsonFileStore.Load<Dictionary<string, Dictionary<string, float[]>>>(path) ?? new()
                : new();
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not read shader parameters '{path}': {exception.Message}");
            _values = new();
        }
    }

    public static ShaderParamStore Default =>
        _default ??= new ShaderParamStore(ProjectSettings.GlobalizePath(UserFile));

    public string Path { get; }

    public float[]? Get(string shader, string uniform) =>
        _values.TryGetValue(shader, out var uniforms) &&
        uniforms.TryGetValue(uniform, out float[]? value)
            ? value
            : null;

    public void Set(string shader, string uniform, float[] value)
    {
        if (!_values.TryGetValue(shader, out var uniforms))
            _values[shader] = uniforms = new Dictionary<string, float[]>();

        uniforms[uniform] = value;
    }

    public void Save() => JsonFileStore.Save(Path, _values);

    /// <summary>Applies every saved value that still matches a uniform of the shader.</summary>
    public void Apply(ShaderMaterial material, string shader, IEnumerable<ShaderUniform> uniforms)
    {
        foreach (ShaderUniform uniform in uniforms)
        {
            float[]? saved = Get(shader, uniform.Name);
            if (saved is not null && saved.Length == uniform.Channels)
                ShaderUniforms.Write(material, uniform, saved);
        }
    }
}
