using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;

namespace Writer.Tools.Animator.Systems;

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

            if (toggle.Value != Get(modes, binding.Mode))
            {
                if (toggle.Value != binding.Synced)
                {
                    Set(ref modes, binding.Mode, toggle.Value);
                    Announce(session, binding.Mode, toggle.Value);
                }
                else
                {
                    toggle.Value = Get(modes, binding.Mode);
                }
            }

            binding.Synced = toggle.Value;
            if (entity.Tags.Has<ToggleChanged>())
                buffer.RemoveTag<ToggleChanged>(entity.Id);
        }
    }

    private static bool Get(in AnimatorModes modes, AnimatorMode mode) => mode switch
    {
        AnimatorMode.AutoKey => modes.AutoKey,
        AnimatorMode.Onion => modes.Onion,
        _ => modes.Rest,
    };

    private static void Set(ref AnimatorModes modes, AnimatorMode mode, bool value)
    {
        switch (mode)
        {
            case AnimatorMode.AutoKey:
                modes.AutoKey = value;
                break;
            case AnimatorMode.Onion:
                modes.Onion = value;
                break;
            default:
                modes.Rest = value;
                break;
        }
    }

    private static void Announce(Entity session, AnimatorMode mode, bool value)
    {
        if (mode == AnimatorMode.Rest)
        {
            session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
            AnimatorSession.SetStatus(session, value ? "REST POSE" : "ANIMATION POSE");
        }
        else
        {
            string name = mode == AnimatorMode.AutoKey ? "AUTO KEY" : "ONION";
            AnimatorSession.SetStatus(session, $"{name} {(value ? "ON" : "OFF")}");
        }
    }
}
