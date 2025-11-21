// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.ColourChannels
{
    public class ChannelUpdateSystem : QuerySystem<ElementEcs,TargetGroupEcs>
    {
        public ChannelUpdateSystem()
        {
            Filter.AnyTags(Tags.Get<SelectionFlag, Dormant, UnInitialized>());
        }


        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref ElementEcs component1, ref TargetGroupEcs component2, Entity _) =>
            {
                component1.Canvas.SetMainColour(component2.GetIndexPrimary() ?? -1);
                component1.Canvas.SetSecondaryColour(component2. GetIndexSecondary() ?? -1);
            }));
        }
    }
}
