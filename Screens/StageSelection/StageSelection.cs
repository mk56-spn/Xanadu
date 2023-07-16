// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {
        [Export]
        private HBoxContainer trackList = null!;

        [Export] private Button startButton = null!;

        private StageInfo activeInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            // Code block filters through the files present in the "Stages" folder and retrieves the stage information for instantiation.
            var dir = DirAccess.Open("res://Resources/Stages/");

            foreach (string? folder in dir.GetDirectories())
            {
                string? file = DirAccess.Open($"{dir.GetCurrentDir()}{folder}").GetFiles().First(f => f.Contains(".tres"));
                var resource = ResourceLoader.Load<StageInfo>($"{dir.GetCurrentDir()}{folder}/{file}");

                trackList.AddChild(new StageSelectionPanel(resource));
            }

            foreach (var panel in trackList.GetChildren().OfType<StageSelectionPanel>())
                panel.FocusEntered += () => activeInfo = panel.StageInfo;

            trackList.GetChild<StageSelectionPanel>(0).GrabFocus();

            startButton.Pressed += () =>
            {
                GetNode<AudioSource>("/root/GlobalAudio").SetTrack(activeInfo.TrackInfo);
                GetTree().ChangeSceneToPacked(activeInfo.Stage);
            };
        }
    }
}
