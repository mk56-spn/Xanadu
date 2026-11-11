using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Advances each character rig's clip playback by the frame time, and turns
/// the clip events it passed into <see cref="CharacterRigEvent"/> entities,
/// clearing the previous frame's first.
/// </summary>
public sealed class CharacterRigAnimateSystem : QuerySystem<CharacterRig>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity previous in Query.Store.Query<CharacterRigEvent>().ToEntityList())
            buffer.DeleteEntity(previous.Id);

        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref rig, entity) =>
        {
            rig.Runtime.Update(delta);
            foreach (XafFiredEvent fired in rig.Runtime.Fired)
            {
                int created = buffer.CreateEntity();
                buffer.AddComponent(created, new CharacterRigEvent(entity, fired.Clip, fired.Name, fired.Time));
            }
        });
    }
}
