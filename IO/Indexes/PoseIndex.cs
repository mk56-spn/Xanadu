using System.Collections.Generic;
using XanaduProject.IO;

namespace XanaduProject.Scenes
{
    public static class PoseIndex
    {
        public static readonly List<Pose> POSES;

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
