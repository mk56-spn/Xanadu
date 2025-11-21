// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.IO.Serialization;

namespace XanaduProject.IO
{
    public static class StagePersistence
    {
        public static StageData GetStage(StageInfo info)
        {
            return new StageData(info, StageDeserializer.Deserialize(info.StagePath));
        }

        public static void SaveStage(StageData data)
        {
            StageSerializer.Serialize(data);
        }

        public static void SaveMetadata(StageData data)
        {
            StageSerializer.SerializeMetadata(data);
        }

        public static void RenameStage(string oldName, string newName)
        {
            string oldPath = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), oldName);
            string newPath = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), newName);

            if (Directory.Exists(oldPath))
            {
                Directory.Move(oldPath, newPath);
            }
        }

        public static StageData LoadCleanStage(string levelName = "New level")
        {
            string dirPath = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR), levelName);

            string metaData = Path.Combine(dirPath, SerializationUtils.METADATA_FILENAME);
            string stagePath = Path.Combine(dirPath, SerializationUtils.ENTITY_STORE_FILENAME);

            var stage = new EntityStore()
            {
                JobRunner = new ParallelJobRunner(10)
            };
            return new StageData(new StageInfo(stagePath, metaData, levelName, [])
            {
                StagePath = levelName,
            }, stage);
        }
    }

    public class StageData(StageInfo stageInfo, EntityStore store)
    {
        public readonly StageInfo StageInfo = stageInfo;
        public readonly EntityStore Store = store;
    }
}
