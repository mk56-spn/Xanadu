// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
	public struct NoteEcs(float timingPoint, NoteType note) : IComponent, IComparable<float>
	{
		public static readonly int RADIUS = 32;

		public readonly NoteType Note = note;

		[Ignore]
		public bool IsHit = false;
		public float TimingPoint = timingPoint;

		public int CompareTo(float other) =>
			TimingPoint.CompareTo(other);

		public readonly struct CreateNote: IEach<NoteEcs, ElementEcs>
		{
			public void Execute(ref NoteEcs noteEcs, ref ElementEcs element)
			{
				GD.PrintRich("[code][color=blue]Note canvas called");
				RenderingServer.CanvasItemAddCircle(element.Canvas, Vector2.Zero, 32, Colors.White);
			}
		}
	}
	public enum NoteType
	{
		Left,
		Right,
		Up,
	}
}
