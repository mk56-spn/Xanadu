using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Projects the left and right Contact stacks into the HUD.</summary>
public sealed class ContactCounterSystem : QuerySystem<ContactCounters, ContactCounterHud>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref counters, ref hud, _) =>
        {
            Set(hud.Left, $"L {counters.Left}", buffer);
            Set(hud.Right, $"R {counters.Right}", buffer);
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
