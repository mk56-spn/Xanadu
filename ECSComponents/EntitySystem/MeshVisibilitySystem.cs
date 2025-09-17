using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.Scenes.MeshEditor;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class MeshVisibilitySystem : QuerySystem<MeshComponent>
    {

        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref MeshComponent component2, Entity entity) =>
            {
                // Check if the entity has the VisibleTag
                // If it has the VisibleTag, ensure the RenderRid is visible
                component2.RenderRid.SetVisible(entity.HasComponent<VisibleTag>());
                // If it does not have the VisibleTag, ensure the RenderRid is hidden
            });
        }
    }
}
