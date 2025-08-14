using Friflo.Engine.ECS;
using Godot;
using XanaduProject.GameDependencies;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public sealed class CanvasCreatorSystem : BaseCreatorSystem
    {
        private readonly Rid master = DiProvider.Get<IVisualsMaster>().GameplayerLayerRid;
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref ElementEcs element, Entity _) =>
            {
                element.Canvas = CanvasItemCreate();
                CanvasItemSetParent(element.Canvas,
                    master);
            });
        }
    }
}
