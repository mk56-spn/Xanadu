// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Composer.Notes;

namespace XanaduProject.Composer
{
    /// <summary>
    /// Handles the input processing for <see cref="HitNote"/>'s
    /// </summary>
    public partial class HitNoteProcessor : Node
    {
        // The area to be checked against for the existence of notes we are overlapping with.
        private Area2D receptor;

        public HitNoteProcessor(Area2D receptor)
        {
            this.receptor = receptor;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!Input.IsActionJustPressed("main")) return;

            var overlappingNotes = getActiveNotes();
            overlappingNotes.FirstOrDefault()?.RequestState(Note.NoteState.Judged);
        }

        private List<HitNote> getActiveNotes()
        {
            return receptor.GetOverlappingAreas().Where(c => c.GetParent() is HitNote)
                .Select(area => area.GetParent<HitNote>())
                .ToList();
        }
    }
}
