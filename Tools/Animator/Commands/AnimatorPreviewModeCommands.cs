using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Entering and leaving preview mode; the layout follows next frame.</summary>
public static class AnimatorPreviewModeCommands
{
    public static bool IsOn(Entity session) =>
        session.TryGetComponent(out AnimatorPreviewMode mode) && mode.On;

    public static void Set(Entity session, bool on)
    {
        if (!session.HasComponent<AnimatorPreviewMode>())
            return;

        ref AnimatorPreviewMode mode = ref session.GetComponent<AnimatorPreviewMode>();
        if (mode.On == on)
            return;

        mode.On = on;
        AnimatorSession.SetStatus(session, on ? "PREVIEW  ·  ESC TO EDIT" : "EDITING");
    }
}
