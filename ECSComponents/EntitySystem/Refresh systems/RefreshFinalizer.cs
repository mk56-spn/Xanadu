// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class RefreshFinalizer : BaseRefreshSystem
    {
        private readonly EntityStore mainStore = DiProvider.Get<EntityStore>();
        private readonly EntityBatch batch = new();
        public RefreshFinalizer()
        {
            batch.RemoveTag<Dormant>();
            mainStore.Entities.ApplyBatch(batch);
        }

        protected override void OnUpdate()
        {
            mainStore.Entities.ApplyBatch(batch);
        }
    }
}
