using System.Collections.Generic;
using Godot;

namespace XanaduProject.Screens.AssetCreation.Skeleton
{
    public record Skeleton
    {
        public string Name { get; set; }
        public List<Vector2> IkTargetPositions { get; set; } = [];
        public List<float> BoneAngles { get; set; } = [];
    }
}
