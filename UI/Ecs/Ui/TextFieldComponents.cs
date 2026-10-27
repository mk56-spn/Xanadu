using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>General ECS text field backing a native text-input bridge.</summary>
public struct TextField(string value, bool numeric = false) : IComponent
{
    public string Value = value;
    public bool Numeric = numeric;
}

/// <summary>Tag for the text field currently receiving keyboard input.</summary>
public struct TextFieldFocused : ITag;
