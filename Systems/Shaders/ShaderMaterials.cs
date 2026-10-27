using Godot;

namespace Xanadu.Systems.Shaders;

public static class ShaderMaterials
{
    /// <summary>Loads a shader as a material, with any saved tuning applied.</summary>
    public static ShaderMaterial? Load(string shaderPath)
    {
        if (GD.Load<Shader>(shaderPath) is not { } shader)
            return null;

        var material = new ShaderMaterial { Shader = shader };
        ShaderParamStore.Default.Apply(material, shaderPath, ShaderUniforms.Describe(shader));
        return material;
    }
}
