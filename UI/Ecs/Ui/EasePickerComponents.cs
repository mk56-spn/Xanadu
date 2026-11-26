using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// The shared ease picker as its owner describes it this frame: whether it is
/// open, what it opens from, and the selection's curve. The owner writes it
/// whenever <see cref="EasePickerFrameDue"/> is emitted on the picker; the
/// picker only reads it. Picks and the × come back as <see cref="EasePicked"/>
/// and <see cref="EasePickerCloseAsked"/>, which the owner applies its own way.
/// </summary>
public struct EasePickerFrame : IComponent
{
    /// <summary>Whether the picker shows.</summary>
    public bool Open;

    /// <summary>The control it opens from, on screen: it sits above it, or below where there is no room.</summary>
    public Rect2 Anchor;

    /// <summary>The area it must stay inside.</summary>
    public Rect2 Bounds;

    /// <summary>The selection's curve, lit in the grid; null when nothing that eases is selected.</summary>
    public EasePreset? Current;

    /// <summary>The hint shown while nothing that eases is selected, such as "Select keys to set their curve".</summary>
    public string EmptyHint;

    /// <summary>Asks <paramref name="picker"/>'s owner to describe it now, and reads what it wrote.</summary>
    public static ref EasePickerFrame Refresh(Entity picker)
    {
        picker.EmitSignal(new EasePickerFrameDue());
        return ref picker.GetComponent<EasePickerFrame>();
    }
}

/// <summary>Write <see cref="EasePickerFrame"/> now.</summary>
public readonly record struct EasePickerFrameDue;

/// <summary>A curve was pressed: set it on the selection. The picker stays open.</summary>
public readonly record struct EasePicked(EasePreset Preset);

/// <summary>The picker's × was pressed.</summary>
public readonly record struct EasePickerCloseAsked;

/// <summary>
/// An ease picker, carried by its panel: its parts and the running preview.
/// </summary>
public struct EasePicker : IComponent
{
    public EasePickerParts Parts;

    /// <summary>Seconds the preview has run, which moves its dots.</summary>
    public float Clock;

    /// <summary>The curve the preview last drew, so it is redrawn only on a change.</summary>
    public EasePreset? Shown;
}

/// <summary>The entities of a picker that its system writes to or reads.</summary>
public struct EasePickerParts
{
    public Entity Close;
    public Entity PreviewBox;
    public Entity Name;
    public Entity Description;
    public Entity Hint;

    /// <summary>The preview's drawing, floating over its box and placed by the system.</summary>
    public Entity Guides;
    public Entity Curve;
    public Entity Rail;
    public Entity Dot;
    public Entity RailDot;
}
