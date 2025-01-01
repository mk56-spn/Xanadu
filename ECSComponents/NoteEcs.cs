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
	public struct NoteEcs(float timingPoint, NoteType note) : IComponent, IComparable<float>, IUpdatable
	{
		public static readonly int RADIUS = 32;

		[Composer("Note")] public readonly NoteType Note = note;

		[Composer("position")]
		public bool CenterPlayer;

		public float TimingPoint = timingPoint;

		public int CompareTo(float other) =>
			TimingPoint.CompareTo(other);

		public void UpdateCharacter(RenderCharacter renderCharacter, ElementEcs elementEcs)
		{
			if (CenterPlayer)
				renderCharacter.Position = elementEcs.Transform.Origin;

			switch (Note)
			{
				case NoteType.Left:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = -750});
					break;
				case NoteType.Up:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ Y = -1200});
					break;
				case NoteType.Right:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = 750});
					break;
				case NoteType.Down:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ Y = 1500});
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}


		public void Update(ElementEcs element)
		{
			float angle = Note switch
			{
				NoteType.Left => -Mathf.Pi,
				NoteType.Right => 0,
				NoteType.Up => -Mathf.Pi / 2,
				NoteType.Down => Mathf.Pi / 2,
				_ => throw new ArgumentOutOfRangeException()
			};

			CanvasItemSetTransform(element.Canvas, element.Transform.RotatedLocal(angle));
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
