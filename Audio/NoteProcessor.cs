// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace XanaduProject.Audio
{
    public partial class NoteProcessor(TrackHandler trackHandler) : Node
    {
        public readonly List<Note> Notes = [];

        public override void _Ready()
        {
            base._Ready();
            Notes.Sort();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            foreach (var note in Notes)
            {
                RenderingServer.CanvasItemSetModulate(note.Canvas, Colors.White  with { A = (float)(1 - Mathf.Abs(note.Element.TimingPoint - trackHandler.TrackPosition)) });
            }
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (!@event.IsActionPressed("main")) return;

            Note note = Notes.First(n => !n.IsHit);
            note.IsHit = true;
            note.HitTime = (float)trackHandler.TrackPosition;
        }
    }
}
