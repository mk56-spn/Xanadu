using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XanaduProject.Scenes;

namespace XanaduProject.IO
{
    public static class PoseIo
    {
        private const string poses_file_path = "poses.json";

        public static void SavePoses(List<Pose> poses)
        {
            string jsonString = JsonSerializer.Serialize(poses);
            File.WriteAllText(poses_file_path, jsonString);
        }

        public static List<Pose> LoadPoses()
        {
            string jsonString = File.ReadAllText(poses_file_path);
            return JsonSerializer.Deserialize<List<Pose>>(jsonString) ?? new List<Pose>();
        }
    }
}
