using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Particles;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// What a tool gives the particle editor: the selected thing's own particle
/// settings (a Xif node's <c>XifParticles</c>), edited in place through the
/// host so its undo records them. The shared <see cref="ParticleLibrary"/>
/// only holds presets: loading one copies it into the settings, storing puts
/// a copy of the settings into it. The editor asks every frame, so an undo
/// that changes the selection or swaps the settings is picked up.
/// </summary>
public interface IParticleEditorHost
{
    /// <summary>The selected thing's particle settings, or null to hide the editor.</summary>
    XpfParticle? Particle { get; }

    /// <summary>What the editor's title calls the thing, such as the part or emitter it belongs to.</summary>
    string Title { get; }

    /// <summary>Gives the selected thing <paramref name="settings"/> in place of its own, as one undo step.</summary>
    void Use(XpfParticle settings);

    /// <summary>Runs one edit of the settings as one undo step and redraws whatever plays them.</summary>
    void Edit(Action change);

    /// <summary>Opens a run of edits, such as a slider drag, that undo as one step.</summary>
    void BeginGesture();

    /// <summary>Closes the run <see cref="BeginGesture"/> opened.</summary>
    void EndGesture();

    /// <summary>The settings changed inside a gesture; redraw without recording a step yet.</summary>
    void Changed();
}

public enum ParticleEditorActionKind
{
    /// <summary>Loads the previous preset in the library into the settings.</summary>
    Previous,

    /// <summary>Loads the next preset in the library into the settings.</summary>
    Next,

    /// <summary>Starts the settings over, plain.</summary>
    New,

    /// <summary>Stores a copy of the settings in the library as a preset under their name, replacing one of that name.</summary>
    Store,

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

    /// <summary>A slider is being dragged, inside one gesture of the host's.</summary>
    public bool Sliding;
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
