// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using static Godot.Mathf;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteHitSystem : QuerySystem<ElementEcs, DirectionEcs>
	{
		protected override void OnUpdate()
		{
			Query.Each(new UpdateHitNote());
		}
	}

	public readonly struct UpdateHitNote : IEach<ElementEcs, DirectionEcs>
	{
		public void Execute(ref ElementEcs element, ref DirectionEcs tempValues)
		{
			float angle = tempValues.Direction switch
			{
				Direction.Left => -Pi,
				Direction.Right => 0,
				Direction.Up => -Pi / 2,
				Direction.UpRight => -Pi / 4,
				Direction.UpLeft => -Pi * (3f / 4),
				Direction.Down => Pi / 2,

				_ => throw new ArgumentOutOfRangeException()
			};

			RenderingServer.CanvasItemSetTransform(element.Canvas, element.Transform.RotatedLocal(angle));
		}
	}
}
