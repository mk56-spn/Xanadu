using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Playfields;

/// <summary>Projects Contact's three bars into the HUD, each as how full it is.</summary>
public sealed class ContactCounterSystem : QuerySystem<ContactCounters, ContactCounterHud>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref counters, ref hud, _) =>
        {
            for (int bar = 0; bar < ContactCounters.Bars; bar++)
                Set(hud[bar], $"{counters[bar]}/{ContactCounters.Cap}", buffer);
        });
    }

    private static void Set(Entity label, string value, CommandBuffer buffer)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;

        ref Text text = ref label.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        buffer.AddTag<Dirty>(label.Id);
    }
}
