// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Composer;
using XanaduProject.Perceptions;
using XanaduProject.Composer.Notes;

namespace XanaduProject.Screens
{
    [GlobalClass]
    public partial class Stage : WorldEnvironment
    {
        public readonly Core Core;

        /// <summary>
        /// Returns the all the notes in the scene;
        /// </summary>
        public List<Note> GetNotes(bool getNestedNotes = true)
        {
            var notes = GetChildren().OfType<Note>().ToList();

            if (!getNestedNotes) return notes;

            foreach (var noteLink in GetChildren().OfType<NoteLink>())
                notes.AddRange(noteLink.GetChildren().OfType<Note>());

            return notes;
        }

        public Stage()
        {
            var coreScene = ResourceLoader.Load<PackedScene>("res://Perceptions/Core.tscn");
            AddChild(Core = coreScene.Instantiate<Core>());
        }
    }
}
