using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Settings;

namespace Writer.Systems.Playfields;

/// <summary>Projects Contact per-input counters into the HUD.</summary>
public sealed class ContactCounterSystem : QuerySystem<ContactCounters, ContactCounterHud>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref counters, ref hud, _) =>
        {
            for (int i = 0; i < hud.Labels.Length; i++)
            {
                Entity label = hud.Labels[i];
                if (label.IsNull || !label.HasComponent<Text>())
                    continue;

                KeybindAction action = KeybindSettings.Actions[i];
                string value = $"{KeybindSettings.LabelFor(action)} {counters.Get(action)}";
                ref Text text = ref label.GetComponent<Text>();
                if (text.Value == value)
                    continue;

                text.Value = value;
                buffer.AddTag<Dirty>(label.Id);
            }
        });
    }
}
