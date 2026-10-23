using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Minimal ECS slider bound to a fill rectangle and label.</summary>
public struct UiSlider(
    Entity fill,
    Entity label,
    float min,
    float max,
    float value) : IComponent
{
    public Entity Fill = fill;
    public Entity Label = label;
    public float Min = min;
    public float Max = max;
    public float Value = value;
}
