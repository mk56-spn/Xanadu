// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Utils
{
    public static class PoseUtils
    {
        public static void CreateTracksForPose()
        {
            var store = GameServices.Store;

            var buffer = store.GetCommandBuffer();
            store.Query<BoneEcs, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref BoneEcs component1, ref NameEcs component2,
                Entity entity) =>
            {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AngleArrayEcs(){ Points = []});
                buffer.AddComponent(i, component2);
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));

            store.Query<IkTargetComponent, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref IkTargetComponent component1, ref NameEcs component2, Entity entity) =>
            {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddComponent(i, new AnimationTarget(entity));
                buffer.AddComponent(i, component2);
            } ));

            store.Query<RootEcs>().ForEachEntity((ref RootEcs _, Entity entity) => {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddComponent(i, new NameEcs("Root Position"));
                buffer.AddComponent(i, new AnimationTarget(entity));
            });

            buffer.Playback();
        }

        public static void CreateTracksForPose(AnimationDto animationDto)
        {
            var store = GameServices.Store;


            var buffer = store.GetCommandBuffer();
            store.Query<BoneEcs, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref BoneEcs component1, ref NameEcs component2,
                Entity entity) =>
            {
                var ecs = component2;
                var track = animationDto.Tracks.Single(c => c.Name == ecs.Name);
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = track.Times});
                buffer.AddComponent(i, new AngleArrayEcs(){ Points = track.FloatValues ?? throw new InvalidOperationException()});
                buffer.AddComponent(i, component2);
                buffer.AddComponent(i, new AnimationTarget(entity));
            } ));

            store.Query<IkTargetComponent, NameEcs>().WithoutAllTags(Tags.Get<IkControlled>()).ForEachEntity(((ref IkTargetComponent component1, ref NameEcs component2, Entity entity) =>
            {
                int i = buffer.CreateEntity();
                var ecs = component2;
                var track = animationDto.Tracks.Single(c => c.Name == ecs.Name);

                buffer.AddComponent(i, new FloatArrayEcs(){ Points = track.Times});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = track.VectorValues ?? throw new InvalidOperationException()});
                buffer.AddComponent(i, new AnimationTarget(entity));
                buffer.AddComponent(i, component2);
            } ));

            store.Query<RootEcs>().ForEachEntity((ref RootEcs root, Entity entity) => {
                int i = buffer.CreateEntity();
                buffer.AddComponent(i, new FloatArrayEcs(){ Points = []});
                buffer.AddComponent(i, new VectorArrayEcs(){ Points = []});
                buffer.AddComponent(i, new NameEcs("Root Position"));
                buffer.AddComponent(i, new AnimationTarget(entity));
            });

            buffer.Playback();
        }
    }


}
