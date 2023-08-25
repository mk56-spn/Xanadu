// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Composer.Notes;

namespace XanaduProject.Composer
{
    public partial class NoteProcessor : Node
    {
        private Area2D receptor;

        public NoteProcessor(Area2D receptor)
        {
            this.receptor = receptor;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!Input.IsActionJustPressed("main")) return;

            List<HitNote> overlappingNotes = receptor.GetOverlappingAreas().Select(area => area.GetParent<HitNote>()).ToList();

            // We want to avoid throwing errors if there is no overlapping areas.
            if (!overlappingNotes.Any()) return;

            // Returns the note with the lowest track position value.
            Notes.Note minNote = overlappingNotes.Aggregate((i, j) => i.PositionInTrack < j.PositionInTrack ? i : j);

            minNote.Activate();
        }
    }
}
