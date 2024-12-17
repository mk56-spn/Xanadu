// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.Rendering;

namespace XanaduProject.Audio
{
	public partial class NoteProcessor(TrackHandler trackHandler, RenderCharacter renderCharacter, EntityStore entityStore) : Node
	{
		private ComponentIndex<HitZoneEcs, Rid> index;

		public override void _EnterTree()
		{
			trackHandler.Stopped += () =>
				entityStore.Query<NoteEcs>().ForEachEntity((ref NoteEcs component1, Entity entity) =>
				{
					component1.IsHit = false;
					RenderingServer.CanvasItemSetModulate(entity.GetComponent<ElementEcs>().Canvas , Colors.Red);
				});

			index = entityStore.ComponentIndex<HitZoneEcs, Rid>();
		}

		public override void _Input(InputEvent @event)
		{
			if (!@event.IsActionPressed("main")) return;

			var query = renderCharacter.QueryShape();

			GD.PrintRich(["[code][color=orange]Returned note count is[color=green] " + query.Length]);
			Rid minBy;

			try {
				minBy = query.Where(c=>  !index[c][0].GetComponent<NoteEcs>().IsHit).MinBy(c => index[c][0].GetComponent<NoteEcs>().TimingPoint);
			}

			catch (Exception) {
				return;
			}


			Entity noteEntity = index[minBy][0];

			ref NoteEcs  note = ref noteEntity.GetComponent<NoteEcs>();
			ref ElementEcs element = ref noteEntity.GetComponent<ElementEcs>();

			note.IsHit = true;

			RenderingServer.CanvasItemSetModulate(element.Canvas, Colors.Blue);


			switch (note.Note)
			{
				case NoteType.Left:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = -750});
					break;
				case NoteType.Up:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ Y = -750});
					break;
				case NoteType.Right:
					renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = 750});
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
