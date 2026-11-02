using Godot;

namespace Xanadu.Systems.Shaders;

public static class ShaderMaterials
{
    /// <summary>Metadata on a material naming which use of its shader it is.</summary>
    public const string InstanceMeta = "tuner_instance";

    /// <summary>Loads a shader as a material.</summary>
    /// <param name="shaderPath">The shader resource to load.</param>
    /// <param name="instance">
    /// A name for this use of the shader, such as "background", which the F8
    /// tuner shows so materials sharing a shader can be told apart.
    /// </param>
    public static ShaderMaterial? Load(string shaderPath, string? instance = null)
    {
        if (GD.Load<Shader>(shaderPath) is not { } shader)
            return null;

        var material = new ShaderMaterial { Shader = shader };
        if (instance is not null)
            material.SetMeta(InstanceMeta, instance);

        return material;
    }
}
