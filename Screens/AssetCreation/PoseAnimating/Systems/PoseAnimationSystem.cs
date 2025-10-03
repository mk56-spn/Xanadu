// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Systems
{
    public class PoseAnimationSystem : QuerySystem
    {
        private readonly EntityStore store = GameServices.Store;
        private readonly Dictionary<Entity, float> defaultAngles = new();
        private readonly Dictionary<Entity, Vector2> defaultVectors = new();

        public PoseAnimationSystem()
        {
            var buffer = store.GetCommandBuffer();
            store.Query<BoneEcs, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref BoneEcs component1, ref NameEcs component2,
                Entity entity) =>
            {
                defaultAngles[entity] = component1.Angle;
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AngleArrayEcs(){ Points = []});
                buffer.AddComponent(i, component2);
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));

            store.Query<IkTargetComponent>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref IkTargetComponent component1,
                Entity entity) =>
            {
                defaultVectors[entity] = component1.TargetPosition;
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddChild(entity.Id, i);
                buffer.AddComponent(i, new AnimationTarget(entity));
                buffer.AddComponent(i, entity.GetComponent<NameEcs>());
            } ));

            store.Query<RootEcs>().ForEachEntity((ref RootEcs root, Entity entity) => {
                defaultVectors[entity] = root.Position;
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
                ref var info = ref PoseAnimatingScreen.Info;
                if (info.AnimationActive == AnimationState.Disabled) return;

                defaultAngles.TryGetValue(target.Entity, out float defaultValue);
                float v = interpolateValue(angleValues.Points, timePoints.Points, defaultValue, ref info);
                target.Entity.GetComponent<BoneEcs>().Angle = v;
            }));

            store.Query<VectorArrayEcs,FloatArrayEcs, AnimationTarget>().ForEachEntity(((ref VectorArrayEcs vectorValues,
                ref FloatArrayEcs timePoints, ref AnimationTarget target, Entity _) =>
            {
                ref var info = ref store.Query<AnimationInfo>().Entities.Single().GetComponent<AnimationInfo>();
                if (info.AnimationActive == AnimationState.Disabled) return;

                defaultVectors.TryGetValue(target.Entity, out var defaultValue);
                Vector2 vector2 = interpolateValue(vectorValues.Points, timePoints.Points, defaultValue, ref info);

                if (target.Entity.HasComponent<RootEcs>())
                    target.Entity.GetComponent<RootEcs>().Position = vector2;
                else
                    target.Entity.GetComponent<IkTargetComponent>().TargetPosition = vector2;
            } ));
        }

        private T interpolateValue<T>(T[] values, float[] timePoints, T defaultValue, ref AnimationInfo info) where T : struct
        {
            if (timePoints.Length == 0)
            {
                return info.DefaultToPose ? defaultValue : values.Length > 0 ? values[0] : defaultValue;
            }

            if (info.LerpBeginningFromPose && info.AnimationPos < timePoints[0])
            {
                float t = info.AnimationPos / timePoints[0];
                return InterpolatorCache<T>.LERP(defaultValue, values[0], t);
            }

            if (info.LerpEndToBeginning && info.AnimationPos > timePoints[^1])
            {
                float t = (info.AnimationPos - timePoints[^1]) / (info.Duration - timePoints[^1]);
                return InterpolatorCache<T>.LERP(values[^1], values[0], t);
            }

            return ColourInterpolatorSystem.LerpedFrameValue<T>(info.AnimationPos, timePoints, values, []);
        }

    }
}
