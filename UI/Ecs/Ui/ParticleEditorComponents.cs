using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Particles;

namespace Xanadu.Ecs;

/// <summary>
/// What a tool gives the particle editor: which library effect the selected
/// thing plays, and how to point it at another one. The effects themselves
/// live in the shared <see cref="ParticleLibrary"/>, which the editor changes
/// directly, so every tool sees an edit at once; the host only records which
/// effect its own thing uses. The editor asks every frame, so an undo that
/// changes the selection is picked up.
/// </summary>
public interface IParticleEditorHost
{
    /// <summary>The name of the effect the selected thing plays, or null to hide the editor.</summary>
    string? Particle { get; }

    /// <summary>What the editor's title calls the thing, such as the effect or emitter it belongs to.</summary>
    string Title { get; }

    /// <summary>Points the selected thing at the effect called <paramref name="name"/>, as one undo step.</summary>
    void Use(string name);
}

public enum ParticleEditorActionKind
{
    /// <summary>Plays the previous effect in the library.</summary>
    Previous,

    /// <summary>Plays the next effect in the library.</summary>
    Next,

    /// <summary>Adds a plain effect and plays it.</summary>
    New,

    /// <summary>Adds a copy of the effect and plays the copy, so the original stays as other things use it.</summary>
    Copy,

    /// <summary>Writes the library to its file.</summary>
    Save,

    /// <summary>Reads the library back from its file, dropping unsaved edits.</summary>
    Revert,

    Texture,
    Shape,
    Upright,
    ScaleCurve,
    Fade,
}

public enum ParticleEditorFieldKind
{
    Name,
    Color,
}

/// <summary>A button of one particle editor, and which editor it belongs to.</summary>
public struct ParticleEditorAction(ParticleEditorActionKind kind, Entity editor) : IComponent
{
    public ParticleEditorActionKind Kind = kind;
    public Entity Editor = editor;
}

/// <summary>A text field of one particle editor; typed values are applied when it loses focus.</summary>
public struct ParticleEditorField(ParticleEditorFieldKind kind, Entity editor) : IComponent
{
    public ParticleEditorFieldKind Kind = kind;
    public Entity Editor = editor;
    public bool WasFocused;
}

/// <summary>
/// Binds a slider of one particle editor to a number of the effect. The system
/// remembers what it last showed, so a slider that moved off it was dragged.
/// </summary>
public struct ParticleEditorSlider(XpfField field, Entity editor) : IComponent
{
    public XpfField Field = field;
    public Entity Editor = editor;
    public float Shown = float.NaN;
}

/// <summary>A particle editor, carried by its panel: the host it edits for and the panel's parts.</summary>
public struct ParticleEditor : IComponent
{
    public IParticleEditorHost Host;

    /// <summary>Where the panel is centred while the host has a selection.</summary>
    public Vector2 Home;

    public ParticleEditorParts Parts;

    /// <summary>The last message, such as why an edit was refused.</summary>
    public string Status;
}

/// <summary>The entities of a particle editor panel that its system writes to or reads.</summary>
public struct ParticleEditorParts
{
    public Entity Title;
    public Entity Name;
    public Entity Save;
    public Entity Texture;
    public Entity Shape;
    public Entity Upright;
    public Entity ScaleCurve;
    public Entity Fade;
    public Entity Color;
    public Entity Status;
    public List<Entity> Sliders;
}
