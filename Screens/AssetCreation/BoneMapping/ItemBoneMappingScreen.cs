using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.BoneMapping.Systems;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens.AssetCreation.BoneMapping
{
    public partial class ItemBoneMappingScreen : PoseScreen
    {
        private static readonly string bone_mapping = "BoneMapping";
        public static ref BoneMappingInfo Info => ref GameServices.Store.GetUniqueEntity(bone_mapping).GetComponent<BoneMappingInfo>();

        private BoneMappingLayout boneMappingLayout;

        public ItemBoneMappingScreen(KeyValuePair<string, StandardPoseSkin> skinInfo)
        {
            CloseTargetScreen = new AssetCreationScreen();
            Store.CreateEntity(new BoneMappingInfo { Skin = skinInfo.Value }, new UniqueEntity(bone_mapping));

            boneMappingLayout = new BoneMappingLayout(this);
            AddChild(boneMappingLayout);
        }

        protected override void PostBaseSystems(SystemRoot root)
        {
            root.Add(new EcsDebugSystem());
            root.Add(new InitializeVisuals(boneMappingLayout.ViewerPanel.ViewerCentre, Info.Skin));
            root.Add(new SelectedBoneSystem());
        }
    }
}
