using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>Toasts each new session status message, as the level editor does.</summary>
public sealed class AnimatorStatusSystem(ToastSystem? toasts = null) : QuerySystem<AnimatorStatus>
{
    private int _shown;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorStatus status = session.GetComponent<AnimatorStatus>();
            _shown = ShellStatus.Show(toasts, status.Message, status.Serial, _shown);
        }
    }
}
