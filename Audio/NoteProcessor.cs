// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Rendering;

namespace XanaduProject.Audio
{
	public partial class NoteProcessor(TrackHandler trackHandler, RenderCharacter renderCharacter) : Node
	{
		public readonly List<Note> Notes = [];

		public override void _Ready()
		{
			Notes.Sort();
			trackHandler.Stopped += () =>
			{
				foreach (var note in Notes)
					note.IsHit = false;
			};
		}

		public override void _Process(double delta)
		{
		}

		public void AddImpulse()
		{

		}

		public override void _Input(InputEvent @event)
		{
			if (!@event.IsActionPressed("main")) return;

			Note? nextNote = Notes.FirstOrDefault(n => !n.IsHit && n.Element.TimingPoint > trackHandler.TrackPosition - 0.3);
			if (nextNote == null || Math.Abs(nextNote.Element.TimingPoint - trackHandler.TrackPosition) > 0.5) return;

			GD.Print(nextNote.Element.Position.DistanceTo(renderCharacter.Position));
			/*`if ( nextNote.Element.Position.DistanceTo(renderCharacter.Position) > 100)return;*/

			renderCharacter.Velocity = new Vector2(renderCharacter.Velocity.X, -5000);
			nextNote.IsHit = true;
			nextNote.HitTime = (float)trackHandler.TrackPosition;

			GD.Print("note is hit");
		}


	}
}
