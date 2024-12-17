// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Rendering;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	public struct BlockEcs : IComponent
	{
        [Ignore]
		public const int COLLISION_FLAG  = 0b00000000_00000000_10000000_00001101;

        [Ignore]
        public Rid Body;
        public void Remove()
        {
            FreeRid(Body);
        }

		public void SetTransform(Transform2D transform)
		{
			BodySetShapeTransform(Body, 0, transform);
		}

		public readonly struct Create(RenderMaster world2D) : IEach<RectEcs, ElementEcs, BlockEcs>
		{
			public void Execute(ref RectEcs rectEcs, ref ElementEcs element, ref BlockEcs body)
			{
				GD.PrintRich("[code][color=Brown]Block canvas called");
				body.Body = createBodyRectangle(element, rectEcs, world2D);
				element.Colour = Colors.Green;
				element.UpdateCanvas();

			}
		}
		private static Rid createBodyRectangle(ElementEcs element, RectEcs rectEcs,RenderMaster world)
		{

            Rid area = BodyCreate();
            Rid shape = RectangleShapeCreate();

            Transform2D transform = Transform2D.Identity;

            BodySetSpace(area, world.GetWorld2D().Space);
            BodyAddShape(area, shape);
            ShapeSetData(shape, rectEcs.Extents / 2);

            BodySetCollisionLayer(area, 0b00000000_00000000_00000000_00001101);
            BodySetCollisionMask(area, 0b00000000_00000000_00000000_00001101);
            BodySetShapeTransform(area, 0, element.Transform);
            BodySetMode(area, BodyMode.Static);

			return area;
		}
	}
}
