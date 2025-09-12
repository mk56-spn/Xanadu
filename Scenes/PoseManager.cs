using Godot;
using System.Collections.Generic;
using System.Linq;

namespace XanaduProject.Scenes
{
    public static class PoseManager
    {
        /// <summary>
        /// Adds a new pose or updates an existing one in the PoseLibrary, then saves the library to the file.
        /// </summary>
        public static void SavePose(string poseName, List<Vector2> ikTargetPositions, List<float> boneAngles)
        {
            if (string.IsNullOrWhiteSpace(poseName))
            {
                GD.PrintErr("Pose name cannot be empty.");
                return;
            }

            var pose = PoseIndex.POSES.FirstOrDefault(p => p.Name == poseName);
            bool isNew = pose == null;
            if (isNew)
            {
                pose = new Pose { Name = poseName };
                PoseIndex.POSES.Add(pose);
            }

            // Assign a fresh copy to avoid external mutations
            pose.IkTargetPositions = new List<Vector2>(ikTargetPositions);
            pose.BoneAngles = new List<float>(boneAngles);

            GD.Print($"{(isNew ? "Adding new" : "Overwriting")} pose: {poseName}");

            PoseIndex.Save();
            GD.Print($"Saving {PoseIndex.POSES.Count} poses to poses.json");
        }
    }
}
