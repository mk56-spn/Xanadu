// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {
        private VBoxContainer trackList = null!;

        public override void _Ready()
        {
            base._Ready();

            trackList = GetNode<VBoxContainer>("TrackList");

            // Code block filters through the files present in the "Stages" folder and retrieves the stage information for instantiation.
            var dir = DirAccess.Open("res://Resources/Stages/");

            foreach (string? folder in dir.GetDirectories())
            {
                string? file = DirAccess.Open($"{dir.GetCurrentDir()}{folder}").GetFiles().First(f => f.Contains(".tres"));
                var resource = ResourceLoader.Load<StageInfo>($"{dir.GetCurrentDir()}{folder}/{file}");

                trackList.AddChild(new StageSelectionPanel(resource));
            }
        }
    }
}
