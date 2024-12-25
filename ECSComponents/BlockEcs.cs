// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Rendering;
using XanaduProject.Tools;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	public struct BlockEcs : IComponent, IUpdatable
	{
        [Ignore]
		public const int COLLISION_FLAG  = 0b00000000_00000000_10000000_00001101;

        [Ignore] public Rid Body { get; private set; }
        public void Remove()
        {
            FreeRid(Body);
        }

		public void SetTransform(Transform2D transform)
		{
			BodySetShapeTransform(Body, 0, transform);
		}

        public void Create(ElementEcs element, RectEcs rectEcs, World2D world2D)
        {
            GD.PrintRich("[code][color=red]Block canvas called");
            Body = createBodyRectangle(element, rectEcs, world2D);
            element.Colour = XanaduColors.XanaduYellow;
        }

		private static Rid createBodyRectangle(ElementEcs element, RectEcs rectEcs,World2D world)
		{

            Rid area = BodyCreate();
            Rid shape = RectangleShapeCreate();

            BodySetSpace(area, world.Space);
            BodyAddShape(area, shape);
            ShapeSetData(shape, rectEcs.Extents / 2);

            BodySetCollisionLayer(area, 0b00000000_00000000_00000000_00001101);
            BodySetCollisionMask(area, 0b00000000_00000000_00000000_00001101);
            BodySetShapeTransform(area, 0, element.Transform);
            BodySetMode(area, BodyMode.Static);

			return area;
		}

        public void Update(ElementEcs elementEcs)
        {
            BodySetShapeTransform(Body, 0, elementEcs.Transform);
        }
    }
}
