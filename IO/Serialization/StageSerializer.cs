// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text.Json;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Serialize;
using Godot;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.IO.Serialization
{
    internal static class StageSerializer
    {
        internal static void  Serialize(StageData data)
        {
            // 1. Extract StageData

            string filename = data.StageInfo.StageName;
            EntityStore store = data.Store;

            // 2. Create directory
            string stageDir = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), filename);
            if (!Directory.Exists(stageDir))
            {
                Directory.CreateDirectory(stageDir);
            }

            // 4. Serialize EntityStore
            shimCreation(store);
            var serializer = new EntitySerializer();
            string entityStorePath = Path.Combine(stageDir, SerializationUtils.ENTITY_STORE_FILENAME);
            using (var writeStream = new FileStream(entityStorePath, FileMode.Create))
            {
                serializer.WriteStore(store, writeStream);
            }

            // 5. Cleanup shims
            var buffer = store.GetCommandBuffer();
            store.Query<ColorArrayEcs>().ForEachEntity((ref ColorArrayEcs _, Entity entity) =>
                buffer.RemoveComponent<ColorArrayThin>(entity.Id));
            buffer.Playback();
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
