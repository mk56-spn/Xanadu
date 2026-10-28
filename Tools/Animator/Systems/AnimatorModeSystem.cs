using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Keeps mode toggles and <see cref="AnimatorModes"/> in step. Whichever side
/// moved away from the last agreed value wins: a click on the toggle sets the
/// mode, and a mode changed by a command moves the toggle. This does not
/// depend on <see cref="ToggleChanged"/> being visible within the same frame.
/// </summary>
public sealed class AnimatorModeSystem : QuerySystem<Toggle, ModeToggle>
{
    protected override void OnUpdate()
    {
        Entity session = AnimatorSession.Find(Query.Store);
        if (session.IsNull)
            return;

        CommandBuffer buffer = CommandBuffer;
        foreach (Entity entity in Query.ToEntityList())
        {
            ref Toggle toggle = ref entity.GetComponent<Toggle>();
            ref ModeToggle binding = ref entity.GetComponent<ModeToggle>();
            ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();

            if (toggle.Value != modes.Get(binding.Mode))
            {
                if (toggle.Value != binding.Synced)
                {
                    modes.Set(binding.Mode, toggle.Value);
                    Announce(session, binding.Mode, toggle.Value);
                }
                else
                {
                    toggle.Value = modes.Get(binding.Mode);
                }
            }

            binding.Synced = toggle.Value;
            if (entity.Tags.Has<ToggleChanged>())
                buffer.RemoveTag<ToggleChanged>(entity.Id);
        }
    }

    /// <summary>Says what the mode change means, the same way a click on its toggle does.</summary>
    public static void Announce(Entity session, AnimatorMode mode, bool value)
    {
        if (mode == AnimatorMode.Rest)
        {
            session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
            AnimatorSession.SetStatus(session, value ? "REST POSE" : "ANIMATION POSE");
        }
        else
        {
            if (mode == AnimatorMode.Svg)
            {
                AnimatorSession.SetStatus(session, value ? "SVG PLACEMENT" : "BONE MOVEMENT");
                return;
            }

            string name = mode switch
            {
                AnimatorMode.AutoKey => "AUTO KEY",
                AnimatorMode.Ik => "IK DRAG",
                AnimatorMode.Pin => "PIN CHILDREN",
                AnimatorMode.Kites => "BONE KITES",
                AnimatorMode.Handles => "HANDLES",
                _ => "ONION",
            };
            AnimatorSession.SetStatus(session, $"{name} {(value ? "ON" : "OFF")}");
        }
    }
}
