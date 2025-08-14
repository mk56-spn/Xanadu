// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public class CreatorFinalizer : BaseCreatorSystem
    {
        private readonly EntityStore mainStore = DiProvider.Get<EntityStore>();

        protected override void OnUpdate() {
            if (Query.Count != 0)
                GD.Print("count" + Query.Count);
            if (Query.Count != 0)
            {
                GD.Print("Refresh 1");

            }
            EntityBatch batch = new EntityBatch().AddTag<Dormant>().RemoveTag<UnInitialized>();
            mainStore.Entities.ApplyBatch(batch);
        }
    }
}
