using Friflo.Engine.ECS;

namespace Xanadu.Model.Animator;

/// <summary>
/// Preview mode: the editing panels give way to the viewport and a preview
/// bar, and the chrome changes colour so the mode is never mistaken for
/// editing. <see cref="AnimatorPreviewModeSystem"/> applies it.
/// </summary>
public struct AnimatorPreviewMode : IComponent
{
    public bool On;

    /// <summary>The state the layout was last put in.</summary>
    public bool Applied;

    /// <summary>The bone drawing modes on entry, put back on exit; preview draws only the art.</summary>
    public bool Kites;
    public bool Handles;

    public static bool IsOn(Entity session) =>
        session.TryGetComponent(out AnimatorPreviewMode mode) && mode.On;
}

/// <summary>Marks a preview bar button, which the preview mode system paints rather than the FLOW tab.</summary>
public struct AnimatorPreviewBarButton : ITag;
