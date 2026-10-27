using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Binds a tuner slider to one channel of a shader uniform.</summary>
public struct ShaderParam(string shader, string uniform, int channel, float defaultValue) : IComponent
{
    public string Shader = shader;
    public string Uniform = uniform;
    public int Channel = channel;
    public float Default = defaultValue;
}

/// <summary>Binds a tuner colour picker to a colour uniform.</summary>
public struct ShaderColorParam(string shader, string uniform, Godot.Color defaultValue) : IComponent
{
    public string Shader = shader;
    public string Uniform = uniform;
    public Godot.Color Default = defaultValue;
}

public enum ShaderTunerAction
{
    Save,
    Reset,
}

/// <summary>A button in the shader tuner panel.</summary>
public struct ShaderTunerButton(ShaderTunerAction action) : IComponent
{
    public ShaderTunerAction Action = action;
}
