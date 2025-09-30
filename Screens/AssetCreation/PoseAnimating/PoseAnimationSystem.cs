// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public class PoseAnimationSystem : QuerySystem
    {
        private readonly EntityStore store = DiProvider.Get<EntityStore>();
        private readonly Dictionary<Entity, float> _defaultAngles = new();
        private readonly Dictionary<Entity, Vector2> _defaultVectors = new();

        public PoseAnimationSystem()
        {
            var buffer = store.GetCommandBuffer();
            store.Query<BoneEcs, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref BoneEcs component1, ref NameEcs component2,
                Entity entity) =>
            {
                _defaultAngles[entity] = component1.Angle;
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AngleArrayEcs(){ Points = []});
                buffer.AddComponent(i, component2);
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));

            store.Query<IkTargetComponent>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref IkTargetComponent component1,
                Entity entity) =>
            {
                _defaultVectors[entity] = component1.TargetPosition;
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddChild(entity.Id, i);
                buffer.AddComponent(i, new AnimationTarget(entity));
                buffer.AddComponent(i, entity.GetComponent<NameEcs>());
            } ));

            store.Query<RootEcs>().ForEachEntity((ref RootEcs root, Entity entity) => {
                _defaultVectors[entity] = root.Position;
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddComponent(i, new NameEcs("Root Position"));
                buffer.AddComponent(i, new AnimationTarget(entity));
            });


            buffer.Playback();
        }

        protected override void OnUpdate()
        {
            store.Query<AngleArrayEcs, FloatArrayEcs, AnimationTarget>().ForEachEntity(((ref AngleArrayEcs angleValues,
                ref FloatArrayEcs timePoints, ref AnimationTarget target, Entity _) =>
            {
                ref var info = ref store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();
                _defaultAngles.TryGetValue(target.Entity, out var defaultValue);

                float v;
                if (timePoints.Points.Length == 0)
                {
                    v = defaultValue;
                }
                else if (info.AnimationPos < timePoints.Points[0])
                {
                    var t = info.AnimationPos / timePoints.Points[0];
                    v = Mathf.Lerp(defaultValue, angleValues.Points[0], t);
                }
                else
                {
                    v = ColourInterpolatorSystem.LerpedFrameValue<float>(info.AnimationPos,
                        timePoints.Points, angleValues.Points,
                        []);
                }
                target.Entity.GetComponent<BoneEcs>().Angle = v;
            }));


            store.Query<VectorArrayEcs,FloatArrayEcs, AnimationTarget>().ForEachEntity(((ref VectorArrayEcs vectorValues,
                ref FloatArrayEcs timePoints, ref AnimationTarget target, Entity _) =>
            {
                ref var info = ref store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();
                _defaultVectors.TryGetValue(target.Entity, out var defaultValue);

                Vector2 vector2;
                if (timePoints.Points.Length == 0)
                {
                    vector2 = defaultValue;
                }
                else if (info.AnimationPos < timePoints.Points[0])
                {
                    var t = info.AnimationPos / timePoints.Points[0];
                    vector2 = defaultValue.Lerp(vectorValues.Points[0], t);
                }
                else
                {
                    vector2 = ColourInterpolatorSystem.LerpedFrameValue<Vector2>(info.AnimationPos, timePoints.Points, vectorValues.Points,
                        []);
                }

                if (target.Entity.HasComponent<RootEcs>())
                {
                    target.Entity.GetComponent<RootEcs>().Position = vector2;
                }
                else if (target.Entity.HasComponent<IkTargetComponent>())
                {
                    target.Entity.GetComponent<IkTargetComponent>().TargetPosition = vector2;
                }
            } ));

            /*store.Query<RootEcs>().ForEachEntity((ref RootEcs root, Entity _) =>
            {
                ref var info = ref store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();

                root.Canvas.SetModulate(Colors.White with { A = 1 - ( 1f * (info.Duration - info.AnimationPos) )});
            });*/
        }
    }
}
