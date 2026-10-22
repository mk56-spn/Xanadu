using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Clickable boolean toggle with configurable on/off labels.</summary>
public struct Toggle(string onText, string offText, bool value) : IComponent
{
    public string OnText = onText;
    public string OffText = offText;
    public bool Value = value;
    public bool Held;
}

/// <summary>Marks a toggle whose value changed this frame.</summary>
public struct ToggleChanged : ITag;
