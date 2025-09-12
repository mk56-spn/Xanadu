
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XanaduProject.Scenes;

namespace XanaduProject.IO
{
    public static class PoseIO
    {
        private const string PosesFilePath = "poses.json";

        public static void SavePoses(List<Pose> poses)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(poses, options);
            File.WriteAllText(PosesFilePath, jsonString);
        }

        public static List<Pose> LoadPoses()
        {
            if (!File.Exists(PosesFilePath))
            {
                return new List<Pose>();
            }

            var jsonString = File.ReadAllText(PosesFilePath);
            return JsonSerializer.Deserialize<List<Pose>>(jsonString);
        }
    }
}
