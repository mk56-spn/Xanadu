using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Binds a tuner slider to one channel of a shader uniform.</summary>
public struct ShaderParam(string uniform, int channel, float defaultValue) : IComponent
{
    public string Uniform = uniform;
    public int Channel = channel;
    public float Default = defaultValue;
}

public enum ShaderTunerAction
{
    Reset,
    Previous,
    Next,
}
