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
        [Export]
        private Button startButton = null!;
        [Export]
        private Button backButton = null!;
        [Export]
        private Button nextButton = null!;

        private StageInfo activeStage = null!;

        public override void _Ready()
        {
            base._Ready();

            loadStages();

            foreach (var stagePanel in trackList.GetChildren().OfType<StageSelectionPanel>())
                stagePanel.FocusEntered += () => activeStage = stagePanel.StageInfo;

            // Sets the initial focused stage.
            trackList.GetChildren().OfType<StageSelectionPanel>().FirstOrDefault()?.GrabFocus();

            startButton.Pressed += () =>
            {
                GetNode<AudioSource>("/root/GlobalAudio").SetTrack(activeStage.TrackInfo);
                GetTree().ChangeSceneToPacked(activeStage.Stage);
            };

            backButton.ButtonUp += () => focus(getFocusedStageIndex() -1);
            nextButton.ButtonUp += () => focus(getFocusedStageIndex() +1);

            void focus(int index)
            {
                if (index < trackList.GetChildCount() || index < 0)
                    trackList.GetChild<StageSelectionPanel>(index).GrabFocus();
            }
        }

        private int getFocusedStageIndex() =>
            trackList.GetChildren()
                .OfType<StageSelectionPanel>()
                .First(c => c.HasFocus()).GetIndex();


        private void loadStages()
        {
            // Code block filters through the files present in the "Stages" folder and retrieves the stage information for instantiation.
            var dir = DirAccess.Open("res://Resources/Stages/");

            foreach (string? folder in dir.GetDirectories())
            {
                string? file = DirAccess.Open($"{dir.GetCurrentDir()}{folder}").GetFiles().First(f => f.Contains(".tres"));
                var resource = ResourceLoader.Load<StageInfo>($"{dir.GetCurrentDir()}{folder}/{file}");

                trackList.AddChild(new StageSelectionPanel(resource));
            }

            GD.Print($"{trackList.GetChildCount()} stages were loaded");
        }
    }
}
