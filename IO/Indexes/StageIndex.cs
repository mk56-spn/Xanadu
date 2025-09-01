// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem;
using Environment = System.Environment;

namespace XanaduProject.IO.Indexes
{
    public static class StageIndex
    {
        public static Dictionary<int, StageInfo> Stages { get; } = new();

        static StageIndex()
        {
            buildIndex();
            GD.Print(string.Join(Environment.NewLine, Stages) + "Stage list");
        }

        private static void buildIndex()
        {
            Stages.Clear();
            string stagesDir = ProjectSettings.GlobalizePath(SerializationUtils.STAGES_DIR);

            if (!Directory.Exists(stagesDir))
            {
                GD.PrintErr($"StageIndex: Could not open directory '{stagesDir}'.");
                return;
            }

            int stageIndex = 0;
            foreach (string dirPath in Directory.EnumerateDirectories(stagesDir))
            {
                string dirName = new DirectoryInfo(dirPath).Name;

                string metadataPath = Path.Combine(dirPath, SerializationUtils.METADATA_FILENAME);
                string entityStorePath = Path.Combine(dirPath, SerializationUtils.ENTITY_STORE_FILENAME);

                if (!File.Exists(metadataPath) || !File.Exists(entityStorePath))
                {
                    GD.PrintErr($"StageIndex: Missing metadata.json or entity_store.json in '{dirPath}'. Skipping stage.");
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(metadataPath);
                    var stageData = JsonSerializer.Deserialize<StageDataEcs>(json);

                    GD.PrintRich(stageData.StageName + "INFO");
                    string stageResPath = SerializationUtils.STAGES_DIR.PathJoin(dirName);
                    var newStageInfo = new StageInfo(){ Stage = stageResPath, StageData = stageData };
                    Stages.Add(stageIndex, newStageInfo);
                    stageIndex++;
                }
                catch (JsonException e)
                {
                    GD.PrintErr($"StageIndex: Failed to parse JSON from '{metadataPath}': {e.Message}");
                }
                catch (IOException e)
                {
                    GD.PrintErr($"StageIndex: Failed to read file '{metadataPath}': {e.Message}");
                }
            }
        }
    }
}
