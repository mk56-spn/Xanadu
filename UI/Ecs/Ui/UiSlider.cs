using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Minimal ECS slider bound to a fill rectangle and label.</summary>
public struct UiSlider(
    Entity fill,
    Entity label,
    float min,
    float max,
    float value,
    string prefix = "") : IComponent
{
    public Entity Fill = fill;
    public Entity Label = label;
    public float Min = min;
    public float Max = max;
    public float Value = value;
    public string Prefix = prefix;
}
