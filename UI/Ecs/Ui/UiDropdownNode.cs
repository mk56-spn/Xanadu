using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// The three parts of a dropdown as nodes: the button that opens it, the list
/// that floats open and the options inside it. It converts to its button, so a
/// dropdown can be chained like any other node.
/// </summary>
public readonly struct UiDropdownNode
{
    private readonly UiSurface _surface;
    private readonly UiNode[] _options;

    internal UiDropdownNode(UiSurface surface, UiDropdownControl control)
    {
        _surface = surface;
        Control = control;
        _options = new UiNode[control.Options.Count];
        for (int i = 0; i < _options.Length; i++)
            _options[i] = new UiNode(surface, control.Options[i]);
    }

    /// <summary>The raw entities, for code that works on the component directly.</summary>
    public UiDropdownControl Control { get; }

    /// <summary>The button that opens the list.</summary>
    public UiNode Button => new(_surface, Control.Button);

    /// <summary>The floating list, which starts closed off-screen.</summary>
    public UiNode List => new(_surface, Control.List);

    /// <summary>The option rows, in order.</summary>
    public IReadOnlyList<UiNode> Options => _options;

    /// <summary>Lets a dropdown be styled and placed as its button.</summary>
    public static implicit operator UiNode(UiDropdownNode dropdown) => dropdown.Button;
}
