// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Factories
{
    public class BoneGroup : SystemGroup
    {
        public BoneGroup(string name) : base(name)
        {
            SkeletonBuilder.BuildPose();

            Add(new BoneTransformSystem());
            Add(new IkSolverSystem());
            Add(new BoneDebugRenderingSystem());
        }
    }
}
