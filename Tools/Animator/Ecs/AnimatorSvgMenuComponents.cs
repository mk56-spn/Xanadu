using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Tools.Animator;

/// <summary>One attachment the SVG picker can add: a file and a group inside it.</summary>
public readonly record struct AnimatorSvgChoice(string Path, string Group)
{
    /// <summary>The file's name, plus the group when the file holds more than one.</summary>
    public string Label
    {
        get
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            return string.Equals(name, Group, System.StringComparison.Ordinal)
                ? name
                : $"{name} / {Group}";
        }
    }
}

/// <summary>
/// The ADD SVG picker opened from the bone menu. It lists every group of every
/// SVG the project holds and adds the chosen one to <see cref="Bone"/>.
/// </summary>
public struct AnimatorSvgMenu : IComponent
{
    public bool Open;

    /// <summary>Screen point the picker was asked to open at, before it is kept on screen.</summary>
    public Vector2 Anchor;

    /// <summary>The bone the chosen attachment is added to.</summary>
    public int Bone;

    /// <summary>Every addable group, refreshed each time the picker opens.</summary>
    public List<AnimatorSvgChoice>? Choices;
}
