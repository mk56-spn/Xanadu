    // Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.EcGui;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EcGuiSetup;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class EcsDebugSystem : QuerySystem
    {
        protected override void OnAddStore(EntityStore  entityStore)
        {
            EcGui.AddExplorerStore("Store", entityStore);
            TypeDrawers.Register();

            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Id));
            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Vector2));
            EcGui.Explorer.AddComponentMemberColumn<ActiveColourEcs>(nameof(ActiveColourEcs.Color));

            var elements = entityStore.Query<ElementEcs>();
            EcGui.AddExplorerQuery("elements", elements);
            var dormant = entityStore.Query<ElementEcs>().AllTags(Tags.Get<Dormant>());
            EcGui.AddExplorerQuery("dormant", dormant);
            var array = entityStore.Query<FloatArrayEcs>();
            EcGui.AddExplorerQuery("arrays", array);
        }

        protected override void OnUpdate()
        {
            EcGui.ExplorerWindow();
            EcGui.InspectorWindow();
        }
    }
}
