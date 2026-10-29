using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Tools.Animator;

/// <summary>
/// Preview mode: the editing panels give way to the viewport and a preview
/// bar, and the chrome changes colour so the mode is never mistaken for
/// editing. <see cref="Systems.AnimatorPreviewModeSystem"/> applies it.
/// </summary>
public struct AnimatorPreviewMode : IComponent
{
    public bool On;

    /// <summary>The state the layout was last put in.</summary>
    public bool Applied;

    /// <summary>The bone drawing modes on entry, put back on exit; preview draws only the art.</summary>
    public bool Kites;
    public bool Handles;

    /// <summary>Where the tab bodies sit while editing; preview parks them.</summary>
    public Vector2 TabBodyCenter;
}

/// <summary>Marks a preview bar button, which the preview mode system paints rather than the FLOW tab.</summary>
public struct AnimatorPreviewBarButton : ITag;
