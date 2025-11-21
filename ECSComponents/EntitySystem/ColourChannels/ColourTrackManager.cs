// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.ColourChannels
{
    public class ColourTrackManager : QuerySystem<ColorArrayEcs>
    {
        private int usedIndex;
        public ColourTrackManager()
        {
            Filter.AllTags(Tags.Get<Dormant>());
        }
        protected override void OnUpdate()
        {
            CommandBuffer buffer =  GameServices.Store.GetCommandBuffer();

            Query.ForEachEntity(((ref ColorArrayEcs _,  Entity entity) =>
            {
                buffer.AddComponent(entity.Id, new IndexEcs(){ Index = usedIndex});
                usedIndex++;

                buffer.RemoveTag<Dormant>(entity.Id);
            } ));

            buffer.Playback();
        }
    }
}
