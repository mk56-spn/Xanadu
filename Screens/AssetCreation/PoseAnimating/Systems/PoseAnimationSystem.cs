// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Tools;
using XanaduProject.Utils;

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
            } ));

            store.Query<IkTargetComponent>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref IkTargetComponent component1,
                Entity entity) =>
            {
                defaultVectors[entity] = component1.TargetPosition;
            } ));

            store.Query<RootEcs>().ForEachEntity((ref RootEcs root, Entity entity) => {
                defaultVectors[entity] = root.Position;
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

        private static T interpolateValue<T>(T[] values, float[] timePoints, T defaultValue, ref AnimationInfo info, EasingType[]? easingTypes = null) where T : struct
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

            return UtilsGeneral.LerpedFrameValue<T>(info.AnimationPos, timePoints, values, easingTypes);
        }

    }
}
