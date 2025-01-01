// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	[ComponentKey(null)]
	public struct HitZoneEcs : IIndexedComponent<Rid>, IUpdatable
	{
		public required Rid Area;
		public Rid GetIndexedValue() => Area;

        public static HitZoneEcs Create(ElementEcs element, World2D world2D) =>
            new() { Area = createAreaRound(element, world2D) };

        private static Rid createAreaRound(ElementEcs element, World2D world)
        {
            var area = AreaCreate();
            var shape = CircleShapeCreate();

            AreaSetSpace(area, world.Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape, 32);

            AreaSetTransform(area, element.Transform);
            AreaSetCollisionLayer(area, 0b00000000_00000000_10000000_00000000);

            return area;
        }

        public void Update(ElementEcs elementEcs)
        {
            AreaSetTransform(Area, elementEcs.Transform);
        }
    }
}
