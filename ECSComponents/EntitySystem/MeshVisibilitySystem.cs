using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.Scenes.ItemEditor;
using MeshComponent = XanaduProject.Screens.AssetCreation.ItemEditor.MeshComponent;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class MeshVisibilitySystem : QuerySystem<MeshComponent>
    {

        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref MeshComponent component2, Entity entity) =>
            {

                component2.RenderRid.SetVisible(entity.Tags.Has<Visible>());
            });
        }
    }
}
