using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

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
