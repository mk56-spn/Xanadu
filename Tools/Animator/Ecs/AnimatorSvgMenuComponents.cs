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

/// <summary>Clicking bones in the preview toggles which of them bend <see cref="Attachment"/>'s mesh.</summary>
public struct AnimatorMeshPick : IComponent
{
    public bool Active;
    public int Attachment;
}

/// <summary>
/// Painting a skin's weights in the preview: <see cref="Attachment"/>'s skin,
/// for <see cref="Bone"/>, with a round brush <see cref="Radius"/> screen
/// pixels across that adds <see cref="Strength"/> a frame at its middle.
/// </summary>
public struct AnimatorWeightPaint : IComponent
{
    public const float MinRadius = 4f;
    public const float MaxRadius = 120f;
    public const float RadiusStep = 4f;
    public const float MinStrength = 0.01f;
    public const float MaxStrength = 0.5f;
    public const float StrengthStep = 0.02f;

    public bool Active;
    public int Attachment;
    public int Bone;
    public float Radius;
    public float Strength;

    /// <summary>Where the pointer last was over the preview, for drawing the brush.</summary>
    public Vector2 Pointer;

    public static AnimatorWeightPaint Default => new() { Attachment = -1, Bone = -1, Radius = 18f, Strength = 0.08f };
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

    /// <summary>The choice is the art of the selected chain, not a new attachment, and closes the picker.</summary>
    public bool ForChain;

    /// <summary>The choice is new alternate art for the selected attachment, and closes the picker.</summary>
    public bool ForArt;

    /// <summary>Every addable group, refreshed each time the picker opens.</summary>
    public List<AnimatorSvgChoice>? Choices;
}
