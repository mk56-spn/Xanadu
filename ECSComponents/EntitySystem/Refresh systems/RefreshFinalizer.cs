// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class RefreshFinalizer : QuerySystem<ElementEcs>
    {
        protected override void OnUpdate()
        {
            Query.AllTags(Tags.Get<Dormant>())
                .ForEachEntity((ref ElementEcs _, Entity entity) => CommandBuffer.RemoveTag<Dormant>(entity.Id));
            CommandBuffer.Playback();
        }
    }
}
