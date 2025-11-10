// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;

namespace XanaduProject.Screens.AssetCreation.BoneMapping.Systems
{
    public class SelectedBoneSystem : QuerySystem<BoneEcs,CanvasEcs,ItemEcs>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref BoneEcs _, ref CanvasEcs component2, ref ItemEcs component3,
                Entity entity) =>
            {
                component2.Canvas.SetSelfModulate(entity.Id == ItemBoneMappingScreen.Info.SelectedBone.Id
                    ? Colors.Yellow
                    : Colors.White);
            } ));
        }
    }
}
