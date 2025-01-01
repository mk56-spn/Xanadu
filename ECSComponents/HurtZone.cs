// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.Colors;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	public struct HurtZoneEcs : IIndexedComponent<Rid>, IUpdatable
    {
        public static readonly Vector2[] TRIANGLE =
        [
            new(-16, 16),
            new(0, -16),
            new(16, 16),
        ];

        [Ignore]
		public required Rid Area;
		public Rid GetIndexedValue() => Area;

        public static HurtZoneEcs Create(ElementEcs element, World2D world2D) =>
            new() { Area = CreateAreaRound(element, world2D) };

        public static Rid CreateAreaRound(ElementEcs element, World2D world)
        {
            var area = AreaCreate();
            var shape = CircleShapeCreate();

            AreaSetSpace(area, world.Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape, 32);

            AreaSetTransform(area, element.Transform);

            AreaSetMonitorable(area, true);
            AreaSetCollisionLayer(area, 0b00000000_10000000_00000000_00000000);

            return area;
        }

        public void Update(ElementEcs elementEcs)
        {
            AreaSetTransform(Area, elementEcs.Transform);
            RenderingServer.CanvasItemAddPolygon(elementEcs.Canvas,TRIANGLE,
                [White.Darkened(0.5f),
                    White,
                    White.Darkened(0.5f)]
            );
        }
    }
}
