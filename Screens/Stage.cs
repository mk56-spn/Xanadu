// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Composer;
using XanaduProject.Perceptions;
using XanaduProject.DataStructure;
using XanaduProject.Composer.Composable.Notes;

namespace XanaduProject.Screens
{
    [GlobalClass]
    public partial class Stage : WorldEnvironment
    {
        public readonly Core Core;

        /// <summary>
        /// General information about this stage.
        /// </summary>
        public StageInfo Info { get; set; } = null!;

        public List<Note> Notes { get; } = new List<Note>();
        public List<NoteLink> NoteLinks { get; private set; } = new List<NoteLink>();

        public Stage()
        {
            ProcessMode = ProcessModeEnum.Always;
            AddChild(Core = Core.CreateCore());
        }

        public override void _EnterTree()
        {
            base._EnterTree();

            GetTree().NodeAdded += node =>
            {
                switch (node)
                {
                    case Note note:
                        Notes.Add(note);
                        break;
                    case NoteLink noteLink:
                        NoteLinks.Add(noteLink);
                        break;
                }
            };
        }

        public override void _Ready()
        {
            base._Ready();

            // Ensure notelinks are sorted sequentially
            NoteLinks = NoteLinks.OrderBy(c => c.OrderedNotes.First()).ToList();
        }
    }
}
