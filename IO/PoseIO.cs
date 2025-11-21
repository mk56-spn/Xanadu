using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XanaduProject.Scenes;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.IO
{
    public static class PoseIo
    {
        private const string poses_file_path = "poses.json";

        public static void SavePoses(List<Skeleton> poses)
        {
            string jsonString = JsonSerializer.Serialize(poses);
            File.WriteAllText(poses_file_path, jsonString);
        }

        public static List<Skeleton> LoadPoses()
        {
            string jsonString = File.ReadAllText(poses_file_path);
            return JsonSerializer.Deserialize<List<Skeleton>>(jsonString) ?? [];
        }
    }
}
