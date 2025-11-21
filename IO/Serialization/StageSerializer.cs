// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text.Json;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Screens;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.IO.Serialization
{
    internal static class StageSerializer
    {
        internal static void  Serialize(StageData data)
        {
            // 1. Extract StageData

            EntityStore serialisingStore = new EntityStore();

           var buffer =  serialisingStore.GetCommandBuffer();


            foreach (var oldEntity in data.Store.Query().WithoutAnyComponents(ComponentTypes.Get<
                         BoneEcs,
                         BoneGlobalTransform,
                         RootEcs,
                         ItemEcs,
                         AnimationInfo,
                         IkTargetComponent>()).Entities)
            {


                var newEntity = serialisingStore.CreateEntity(id: oldEntity.Id);
                oldEntity.CopyEntity(newEntity);

                if (newEntity.Tags.Has<SelectionFlag>())
                    buffer.RemoveTag<SelectionFlag>(newEntity.Id);


            }

            buffer.Playback();

            string filename = data.StageInfo.StageName;

            // 2. Create directory
            string stageDir = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), filename);
            if (!Directory.Exists(stageDir))
            {
                Directory.CreateDirectory(stageDir);
            }

            // 4. Serialize EntityStore
            shimCreation(serialisingStore);
            var serializer = new EntitySerializer();
            string entityStorePath = Path.Combine(stageDir, SerializationUtils.ENTITY_STORE_FILENAME);
            using var writeStream = new FileStream(entityStorePath, FileMode.Create);
            serializer.WriteStore(serialisingStore, writeStream);
        }

        public static void SerializeMetadata(StageData data)
        {
            string filename = data.StageInfo.StageName;
            string stageDir = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), filename);


            string metadataPath = Path.Combine(stageDir, SerializationUtils.METADATA_FILENAME);
            using var writeStream = new FileStream(metadataPath, FileMode.Create);
            JsonSerializer.Serialize(writeStream, data.StageInfo);
        }
        private static void shimCreation(EntityStore store)
        {
            var buffer = store.GetCommandBuffer();
            store.Query<ColorArrayEcs>().ForEachEntity((ref ColorArrayEcs component1, Entity entity) =>
                buffer.AddComponent(entity.Id, new ColorArrayThin(component1)));

            buffer.Playback();
        }
    }
}
