// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public class PoseAnimationSystem : QuerySystem
    {
        private readonly EntityStore store = DiProvider.Get<EntityStore>();

        public PoseAnimationSystem()
        {
            var buffer = store.GetCommandBuffer();
            store.Query<BoneEcs, NameEcs>().ForEachEntity(((ref BoneEcs component1, ref NameEcs component2,
                Entity entity) =>
            {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AngleArrayEcs(){ Points = []});
                buffer.AddComponent(i, component2);
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));

            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component1,
                Entity entity) =>
            {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));


            buffer.Playback();
        }

        protected override void OnUpdate()
        {

            store.Query<AngleArrayEcs,FloatArrayEcs, AnimationTarget>().ForEachEntity(((ref AngleArrayEcs component1,
                ref FloatArrayEcs component2, ref AnimationTarget target, Entity _) =>
            {
                float v = ColourInterpolatorSystem.LerpedFrameValue<float>((DateTime.Now.Second / 1000f) % 2, component2.Points, component1.Points,
                    []);
                target.Entity.GetComponent<BoneEcs>().Angle = v;
            } ));


            store.Query<VectorArrayEcs,FloatArrayEcs, AnimationTarget>().ForEachEntity(((ref VectorArrayEcs component1,
                ref FloatArrayEcs component2, ref AnimationTarget target, Entity _) =>
            {
                Vector2 vector2 = ColourInterpolatorSystem.LerpedFrameValue<Vector2>((DateTime.Now.Second / 1000f) % 2, component2.Points, component1.Points,
                    []);
                target.Entity.GetComponent<IkTargetComponent>().TargetPosition = vector2;
            } ));        }
    }
}
