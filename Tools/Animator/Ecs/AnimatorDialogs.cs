using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The clip length field and its six step buttons, opened from the CONFIG
/// menu. The panel floats under the header so the timeline stays visible.
/// </summary>
public struct AnimatorClipLength : IComponent
{
    public bool Open;
}

/// <summary>
/// The NEW CLIP dialog: whether it is open. Its name, length and duplicate
/// choice live in the UI fields, and creating the clip reads them.
/// </summary>
public struct AnimatorNewClipDialog : IComponent
{
    public bool Open;
}

/// <summary>
/// The EASING tool window. It stays open while keys are picked on the
/// timeline, so it has no dimmer and a click outside leaves it be.
/// </summary>
public struct AnimatorEasingWindow : IComponent
{
    public bool Open;

    /// <summary>Seconds the preview has run, which moves its dots.</summary>
    public float Clock;

    /// <summary>The curve the preview last drew, so it is redrawn only on a change.</summary>
    public XafEasePreset? Shown;
}

/// <summary>Marks a curve thumbnail in the EASING window.</summary>
public struct AnimatorEasingCell : ITag;

/// <summary>The file info popup, centred over a dimmer; a click outside it closes it.</summary>
public struct AnimatorInfoPanel : IComponent
{
    public bool Open;
}

/// <summary>
/// The key menu opened by right-clicking a track row in the timeline, and
/// where in the clip it was opened, which is what ADD KEY keys.
/// </summary>
public struct AnimatorKeyMenu : IComponent
{
    public bool Open;

    /// <summary>Screen point of the menu's top-left corner.</summary>
    public Vector2 Anchor;

    /// <summary>The bone or attachment whose row was clicked.</summary>
    public XafTrack Track;

    /// <summary>The channel row clicked, or null for the target's merged row.</summary>
    public XafChannelProperty? Property;

    /// <summary>Seconds into the clip under the pointer, snapped, or the time of the key clicked.</summary>
    public float Time;
}
