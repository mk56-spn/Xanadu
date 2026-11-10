using Friflo.Engine.ECS;
using Xanadu.Timeline;

namespace Xanadu.Ecs;

/// <summary>
/// What a tool tells the shared ease picker: whether it is open, what it opens
/// from, and the selection's curve. Picking and closing go back to the tool,
/// which applies the curve as one undo step its own way.
/// </summary>
public interface IEasePickerHost
{
    /// <summary>Whether the picker shows.</summary>
    bool Open { get; }

    /// <summary>The control it opens from, on screen: it sits above it, or below where there is no room.</summary>
    Rect2 Anchor { get; }

    /// <summary>The area it must stay inside.</summary>
    Rect2 Bounds { get; }

    /// <summary>The selection's curve, lit in the grid; null when nothing that eases is selected.</summary>
    EasePreset? Current { get; }

    /// <summary>The hint shown while nothing that eases is selected, such as "Select keys to set their curve".</summary>
    string EmptyHint { get; }

    /// <summary>Sets the curve on the selection. The picker stays open, so the selection can change in turn.</summary>
    void Pick(EasePreset preset);

    /// <summary>The picker's × was pressed.</summary>
    void Close();

    /// <summary>Anything a tool adds to a curve's tooltip, such as its shortcut key.</summary>
    string TooltipNote(EasePreset preset) => "";
}

/// <summary>
/// An ease picker, carried by its panel: the host it picks for, its parts and
/// the running preview.
/// </summary>
public struct EasePicker : IComponent
{
    public IEasePickerHost Host;
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

/// <summary>A curve thumbnail of one picker; pressing it picks the curve.</summary>
public struct EasePickerCell(EasePreset preset, Entity picker) : IComponent
{
    public EasePreset Preset = preset;
    public Entity Picker = picker;
}
