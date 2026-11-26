using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Particles;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// The particle editor as its owner describes it: the selected thing's own
/// particle settings (a Xif node's <c>XifParticles</c>), edited in place and
/// recorded on the owner's undo through the shared edit signals
/// (<see cref="EditAsked"/>), and <see cref="ParticleUseAsked"/> to swap them.
/// The shared <see cref="ParticleLibrary"/> only holds presets: loading one
/// copies it into the settings, storing puts a copy of the settings into it.
/// The owner writes this whenever <see cref="ParticleEditorFrameDue"/> is
/// emitted, so an undo that changes the selection or swaps the settings is picked up.
/// </summary>
public struct ParticleEditorFrame() : IComponent
{
    /// <summary>The selected thing's particle settings, or null to hide the editor.</summary>
    public XpfParticle? Particle;

    /// <summary>What the editor's title calls the thing, such as the part or emitter it belongs to.</summary>
    public string Title = "";

    /// <summary>Asks the owner of the editor on <paramref name="panel"/> to describe it now, and reads what it wrote.</summary>
    public static ParticleEditorFrame Refresh(Entity panel)
    {
        panel.EmitSignal(new ParticleEditorFrameDue());
        return panel.GetComponent<ParticleEditorFrame>();
    }
}

/// <summary>Write <see cref="ParticleEditorFrame"/> now.</summary>
public readonly record struct ParticleEditorFrameDue;

/// <summary>Give the selected thing <paramref name="Settings"/> in place of its own, as one undo step.</summary>
public readonly record struct ParticleUseAsked(XpfParticle Settings);

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

/// <summary>A particle editor, carried by its panel: the panel's parts and what it is doing.</summary>
public struct ParticleEditor : IComponent
{

    /// <summary>Where the panel is centred while the owner has a selection.</summary>
    public Vector2 Home;

    public ParticleEditorParts Parts;

    /// <summary>The last message, such as why an edit was refused.</summary>
    public string Status;

    /// <summary>A slider is being dragged, inside one gesture of the owner's.</summary>
    public bool Sliding;
}

/// <summary>The entities of a particle editor panel that its system writes to or reads.</summary>
public struct ParticleEditorParts
{
    /// <summary>The panel itself, which the owner's signals are emitted on.</summary>
    public Entity Panel;

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
