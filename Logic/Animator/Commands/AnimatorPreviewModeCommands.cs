using Friflo.Engine.ECS;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Entering and leaving preview mode; the layout follows next frame.</summary>
public static class AnimatorPreviewModeCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<TogglePreviewMode>((s, _) => Set(s, !s.GetComponent<AnimatorPreviewMode>().On));
        session.On<ExitPreviewMode>((s, _) =>
        {
            // Esc closes the XIF editor first, when it is open.
            Entity xif = s.GetComponent<AnimatorUi>().XifEditor;
            if (XifEditorSystem.IsOpen(xif))
                XifEditorSystem.Close(xif);
            else
                Set(s, false);
        });
    }

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
