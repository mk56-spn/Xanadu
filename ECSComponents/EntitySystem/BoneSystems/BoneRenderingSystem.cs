// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneDebugRenderingSystem :QuerySystem<RootEcs>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref RootEcs rootEcs, Entity entity)  =>
            {
                rootEcs.Canvas.Clear();
                foreach (var v in entity.GetIncomingLinks<BoneGlobalTransform>())
                {
                    var bone = v.Entity.GetComponent<BoneEcs>();
                    ref BoneGlobalTransform boneGlobalTransform = ref v.Component;

                    var jointPos = boneGlobalTransform.GlobalPosition;
                    var boneVec = new Vector2(bone.Length, 0).Rotated(boneGlobalTransform.GlobalAngle);
                    var boneEndPos = jointPos + boneVec;

                    var boneNormal = boneVec.Normalized().Rotated(Mathf.Pi / 2.0f);
                    float baseWidth = 8.0f;

                    var p1 = jointPos + boneNormal * baseWidth / 2.0f;
                    var p2 = jointPos - boneNormal * baseWidth / 2.0f;

                    var points = new[] { p1, boneEndPos, p2 };
                    var color = new Color(0, 0, 1, 0.5f);

                    rootEcs.Canvas.AddPolygon(points, [color, color, color]);
                }
            });
        }
    }
}
