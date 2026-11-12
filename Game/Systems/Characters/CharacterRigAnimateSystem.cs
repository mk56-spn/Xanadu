using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Advances each character rig's clip playback by the frame time. The clip
/// events it passed this frame are on <see cref="AnimatorRuntime.Fired"/>.
/// </summary>
public sealed class CharacterRigAnimateSystem : QuerySystem<CharacterRig>
{
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref rig, _) => rig.Runtime.Update(delta));
    }
}
