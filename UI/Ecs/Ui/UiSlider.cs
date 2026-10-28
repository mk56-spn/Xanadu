using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Minimal ECS slider bound to a fill rectangle and label.</summary>
public struct UiSlider(
    Entity fill,
    Entity label,
    float min,
    float max,
    float value,
    string prefix = "",
    string format = "0",
    float? defaultValue = null) : IComponent
{
    public Entity Fill = fill;
    public Entity Label = label;
    public float Min = min;
    public float Max = max;
    public float Value = value;
    public string Prefix = prefix;

    /// <summary>Number format for the value shown in the label.</summary>
    public string Format = format;

    /// <summary>Value restored by a right-click on the track.</summary>
    public float Default = defaultValue ?? value;
}
