// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Character;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.BoneSystems
{
    public class BoneRenderingSystem :QuerySystem<BoneGlobalTransform, BoneEcs>
    {
        private readonly IPlayerCharacter master = DiProvider.Get<IPlayerCharacter>();
        private readonly RenderRid canvas = RenderRid.Create();
        private readonly RenderRid canvasBack = RenderRid.Create();

        public BoneRenderingSystem()
        {
            canvasBack.SetParent(master.PlayerCanvasRid)
                .SetTransform(new Transform2D(0, new Vector2(0, -PlayerCharacter.CHARACTER_HEIGHT /2) ))
                .AddChild(canvas);
        }
        protected override void OnUpdate()
        {
            canvas.Clear();
            canvasBack.Clear();

            Query.ForEachEntity((ref BoneGlobalTransform component1, ref BoneEcs component2, Entity entity) =>
            {
                var endPoint = component1.GlobalPosition + new Vector2(component2.Length, 0).Rotated(component1.GlobalAngle);

                int i = 0;

                climbTree(component2);

                // Replace bone line with a filled triangle using RenderingServer primitive.
                var dir = (endPoint - component1.GlobalPosition).Normalized();
                var perp = dir.Orthogonal();
                float baseSize = Mathf.Clamp(component2.Length * 0.25f, 6f, 18f);
                float headLen = component2.Length;
                float headWidth = baseSize * 0.6f;

                var tip = endPoint;
                var baseCenter = tip - dir * headLen;
                var leftBase = baseCenter + perp * (headWidth * 0.5f);
                var rightBase = baseCenter - perp * (headWidth * 0.5f);

                var color = Colors.White.Darkened(0.3f * i);
                var colors = new[] { color, color, color };

                // Use AddPrimitive directly (no triangle-specific method).
                canvasBack.AddPrimitive(new[] { leftBase, rightBase, tip }, colors);

                // Keep joint visualization.
                canvas.AddCircle(5, position: component1.GlobalPosition, Colors.CornflowerBlue.Darkened(0.3f * i));
                return;

                void climbTree(BoneEcs e)
                {
                    while (true)
                    {
                        if (e.ParentEntity.IsNull) return;
                        i++;
                        e = e.ParentEntity.GetComponent<BoneEcs>();
                    }
                }
            });
        }
    }
}
