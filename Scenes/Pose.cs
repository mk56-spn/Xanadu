using System.Collections.Generic;
using Godot;

namespace XanaduProject.Scenes
{
    public record Pose
    {
        public string Name { get; set; }
        public List<Vector2> IkTargetPositions { get; set; } = [];
        public List<float> BoneAngles { get; set; } = [];
    }
}
