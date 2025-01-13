// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Rendering;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents
{
	public struct NoteEcs(float timingPoint) : IComponent, IComparable<float>, IUpdatable
	{
		public static readonly int RADIUS = 32;

		[Composer("position")]
		public bool CenterPlayer;

		public float TimingPoint = timingPoint;

		public int CompareTo(float other) =>
			TimingPoint.CompareTo(other);


		public void Update(ElementEcs element)
		{
			CanvasItemSetSelfModulate(element.Canvas, Colors.Violet);
		}
	}
	public enum NoteType
	{
		Left,
		Right,
		Up,
		Down
	}
}
