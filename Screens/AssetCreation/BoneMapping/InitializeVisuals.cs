// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public class InitializeVisuals : QuerySystem
    {
        private readonly Control centre;


        public InitializeVisuals(Control centre)
        {
            this.centre = centre;
            Filter.AllTags(Tags.Get<Dormant>());
        }

        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);
            store.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
                rootEcs.Canvas.SetParent(centre));
        }

        protected override void OnUpdate(){}
    }
}
