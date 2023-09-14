// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;
using XanaduProject.Composer.Notes;
using XanaduProject.Perceptions;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens
{
    [GlobalClass]
    public partial class Stage : WorldEnvironment
    {
        public readonly Core Core;

        // General information about this stage.
        public StageInfo Info { get; set; } = null!;

        public List<Note> Notes { get; } = new List<Note>();

        public Stage()
        {
            ProcessMode = ProcessModeEnum.Always;

            var coreScene = ResourceLoader.Load<PackedScene>("res://Perceptions/Core.tscn");
            AddChild(Core = coreScene.Instantiate<Core>());
        }

        public override void _EnterTree()
        {
            base._EnterTree();

            GetTree().NodeAdded += node =>
            {
                if (node is Note note)
                    Notes.Add(note);
            };
        }
    }
}
