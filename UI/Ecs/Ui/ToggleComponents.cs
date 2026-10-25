using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Clickable boolean toggle with configurable on/off labels and colors.</summary>
public struct Toggle : IComponent
{
    public string OnText;
    public string OffText;
    public bool Value;
    public bool Held;
    public Color OnColor;
    public Color OffColor;

    public Toggle(
        string onText,
        string offText,
        bool value,
        Color? onColor = null,
        Color? offColor = null)
    {
        OnText = onText;
        OffText = offText;
        Value = value;
        Held = false;
        OnColor = onColor ?? new Color("#2563eb");
        OffColor = offColor ?? new Color("#334155");
    }
}

/// <summary>Marks a toggle whose value changed this frame.</summary>
public struct ToggleChanged : ITag;
