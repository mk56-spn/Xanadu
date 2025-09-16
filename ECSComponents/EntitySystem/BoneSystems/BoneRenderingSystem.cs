// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneRenderingSystem :QuerySystem<RootEcs>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref RootEcs component1, Entity entity)  =>
            {
                component1.Canvas.Clear();
                foreach (var v in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    var bone = v.Entity.GetComponent<BoneEcs>();
                    ref BoneGlobalTransform boneGlobalTransform = ref v.Component;
                    component1.Canvas.AddCircle(2, position:boneGlobalTransform.GlobalPosition, color: new Color(1, 0, 0));
                    component1.Canvas.AddLine(boneGlobalTransform.GlobalPosition,
                        boneGlobalTransform.GlobalPosition + new Vector2(bone.Length, 0).Rotated(boneGlobalTransform.GlobalAngle), Colors.Red);
                }
            });
        }
    }
}
