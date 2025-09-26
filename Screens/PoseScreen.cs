// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens
{
    public partial class PoseScreen : StoreScreen
    {
        public PoseScreen()
        {
            SkeletonBuilder.BuildPose();

            Root.Add(new BoneTransformSystem());
            Root.Add(new IkSolverSystem());
            Root.Add(new BoneRenderingSystem());
        }
    }
}
