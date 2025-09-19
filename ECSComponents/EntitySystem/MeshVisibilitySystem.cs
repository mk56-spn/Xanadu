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

                component2.RenderRid.SetVisible(entity.Tags.Has<Visible>());
                // If it does not have the Visible, ensure the RenderRid is hidden
            });
        }
    }
}
