using Friflo.Engine.ECS;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

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
    public RigTrack Track;

    /// <summary>The channel row clicked, or null for the target's merged row.</summary>
    public RigChannelProperty? Property;

    /// <summary>Seconds into the clip under the pointer, snapped, or the time of the key clicked.</summary>
    public float Time;

    /// <summary>The override menu, opened instead when the right-click lands on an override's bar.</summary>
    public bool OverrideOpen;

    /// <summary>The override whose bar was right-clicked.</summary>
    public RigLimbTarget? Override;
}
