using Friflo.Engine.ECS;
using Xanadu.Core;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>General ECS text field backing a native text-input bridge.</summary>
public struct TextField(string value, bool numeric = false) : IComponent
{
    public string Value = value;
    public bool Numeric = numeric;

    /// <summary>The fill while not focused; null keeps the inset colour.</summary>
    public Color? Fill;
}

/// <summary>Tag for the text field currently receiving keyboard input.</summary>
public struct TextFieldFocused : ITag;
