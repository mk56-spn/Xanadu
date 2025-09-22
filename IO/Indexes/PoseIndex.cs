using System.Collections.Generic;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Scenes
{
    public static class PoseIndex
    {
        public static readonly List<Skeleton> POSES;

        static PoseIndex()
        {
            POSES = PoseIo.LoadPoses();
        }

        public static void Save()
        {
            PoseIo.SavePoses(POSES);
        }
    }
}
