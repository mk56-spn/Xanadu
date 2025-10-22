using Friflo.Engine.ECS;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public struct BoneMappingInfo : IComponent
    {
        public StandardPoseSkin Skin;
        public string SkinName;
        public string SelectedBone;
        public bool SaveSkinTrigger;
    }
}
