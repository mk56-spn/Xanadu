using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor.Analysis;

/// <summary>The parts of a note the analysers need, in playfield coordinates.</summary>
public readonly record struct AnalysedNote(
    long TimeMs,
    Vector2 Position,
    NoteKind Kind,
    DashStrength Strength = DashStrength.Single,
    bool Diagonal = false);
