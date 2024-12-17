// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	public struct NoteEcs(float timingPoint, int direction) : IComponent, IComparable<float>
	{
		public static readonly int RADIUS = 32;
		public bool IsHit = false;

		public float TimingPoint = timingPoint;
		public Vector2 Angle = Vector2.FromAngle(direction * Mathf.Pi / 2);

		public int CompareTo(float other) =>
			TimingPoint.CompareTo(other);

		public readonly struct CreateNote(World2D world2D) : IEach<NoteEcs, ElementEcs, AreaEcs>
		{
			public void Execute(ref NoteEcs noteEcs, ref ElementEcs element, ref AreaEcs area)
			{
				GD.PrintRich("[code][color=blue]Note canvas called");
				area.Area = createAreaRound(element, world2D);
				RenderingServer.CanvasItemAddCircle(element.Canvas, Vector2.Zero, 32, Colors.White);
			}
		}
		private static Rid createAreaRound(ElementEcs element, World2D world)
		{
			var area = AreaCreate();
			var shape = RectangleShapeCreate();

			AreaSetSpace(area, world.Space);
			AreaAddShape(area, shape);
			ShapeSetData(shape, new Vector2(60,54) / 2);

			AreaSetTransform(area, element.Transform);
			AreaSetCollisionLayer(area, 0b00000000_00000000_10000000_00000000);
			AreaSetCollisionLayer(area, 0b00000000_00000000_10000000_00000000);

			return area;
		}
	}
}
