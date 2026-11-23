using System.Collections.Generic;

namespace Xanadu.Core.Keys;

/// <summary>What a keyed field holds, which decides how it is sampled.</summary>
public enum KeyValueKind : byte
{
    /// <summary>A number eased between keys.</summary>
    Number,

    /// <summary>An angle in radians, eased between keys.</summary>
    Angle,

    /// <summary>On (1) or off (0); held, never eased.</summary>
    Switch,

    /// <summary>A whole number, such as which art or which layer; held.</summary>
    Index,
}

/// <summary>How what keys a field (a clip, then groups over it) lays over the node's own value.</summary>
public enum KeyCombine : byte
{
    /// <summary>The keyed value takes the node's place; with several, the last wins.</summary>
    Replace,

    /// <summary>Added to the node's value, as a move is.</summary>
    Add,

    /// <summary>Multiplies the node's value, as a fade or a tint does.</summary>
    Multiply,
}

/// <summary>
/// A field a module lets clips and groups key: its name (what addresses it),
/// what it holds, how keys lay over it, and the short label a timeline row shows.
/// </summary>
public readonly record struct KeyField(string Name, KeyValueKind Kind, KeyCombine Combine, string Label = "")
{
    /// <summary>The label a row shows: <see cref="Label"/>, or the name.</summary>
    public string Shown => Label.Length > 0 ? Label : Name;

    /// <summary>Held between keys rather than eased.</summary>
    public bool Held => Kind is KeyValueKind.Switch or KeyValueKind.Index;
}

/// <summary>
/// A module whose fields can be keyed. <see cref="KeyFields"/> lists the
/// node's own; <see cref="PartFields"/> those of each element inside it (a
/// skeleton's bones), with <see cref="Parts"/> naming the elements. Whatever
/// keys (rig clips now, level groups later) targets only what is listed here,
/// so a new module is keyable everywhere by declaring its fields.
/// </summary>
public interface IKeyable
{
    IReadOnlyList<KeyField> KeyFields => [];

    IReadOnlyList<KeyField> PartFields => [];

    IEnumerable<string> Parts => [];
}
