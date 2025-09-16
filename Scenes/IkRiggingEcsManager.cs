using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using System.Collections.Generic;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes
{
    public class IkRiggingEcsManager
    {
        private readonly EntityStore entityStore;
        private readonly SystemRoot systemRoot;

        public IkRiggingEcsManager()
        {
            entityStore = DiProvider.Get<EntityStore>();
            systemRoot = new SystemRoot
            {
                new BoneTransformSystem(),
                new IkSolverSystem(),
                new BoneRenderingSystem(),
            };
            systemRoot.AddStore(entityStore);
        }

        public void SetupInitialScene(List<Entity> allBones, List<Entity> ikTargetEntities)
        {
          /*  var rootEntity = PoseBuilder.BuildPose(allBones, ikTargetEntities);
            var rootComponent = rootEntity.GetComponent<RootEcs>();
            rootComponent.Canvas.SetParent(DiProvider.Get<IVisualsMaster>().GameplayerLayerRid);
            rootComponent.Canvas.SetTransform(new Transform2D(0, new Vector2(500, 500)));*/
        }

        public ArchetypeQuery<IkTargetComponent> GetIkTargetsQuery()
        {
            return entityStore.Query<IkTargetComponent>();
        }

        public ArchetypeQuery<BoneEcs, BoneGlobalTransform> GetBonesQuery()
        {
            return entityStore.Query<BoneEcs, BoneGlobalTransform>();
        }

        public void Update()
        {
            systemRoot.Update(default);
        }
    }
}
