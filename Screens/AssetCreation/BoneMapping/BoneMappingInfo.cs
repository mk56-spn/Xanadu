using Friflo.Engine.ECS;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public struct BoneMappingInfo : IComponent
    {
        public StandardPoseSkin Skin;
        public Entity SelectedBone;

        public string SelectedItemName => SelectedBone.GetComponent<NameEcs>().Name;

        public bool SaveSkinTrigger;
    }
}
