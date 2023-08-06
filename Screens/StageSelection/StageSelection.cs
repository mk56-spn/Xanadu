// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Screens.Player;
using XanaduProject.Singletons;

namespace XanaduProject.Screens.StageSelection
{
    public partial class StageSelection : Control
    {
        private const double transition = 0.5;

        [Export]
        private HBoxContainer trackList = null!;

        [Export] private Button startButton = null!;
        [Export] private ScrollContainer scrollContainer = null!;

        private StageInfo activeInfo = null!;

        private Tween? opacityTween;

        public override void _Ready()
        {
            base._Ready();

            trackList.AddThemeConstantOverride("separation", 100);

            var dir = DirAccess.Open("res://Resources/Stages/");

            foreach (string? folder in dir.GetDirectories())
            {
                // Retrieves information for the subsequent loading of it's stage.
                StageInfo stageInfo = ResourceLoader.Load<StageInfo>($"res://Resources/Stages/{folder}/information.tres");

                trackList.AddChild(new StageSelectionPanel(stageInfo));
            }

            Tween? scrollTween = null;

            // TODO : this should all be moved to its own class along with all the stage selection carousel stuff honestly.
            foreach (var panel in trackList.GetChildren().OfType<StageSelectionPanel>())
                panel.FocusEntered += () =>
                {
                    scrollTween = CreateTween();
                    scrollTween.TweenProperty(scrollContainer, "scroll_horizontal", panel.Position.X + panel.Size.X / 2,
                            transition)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);

                    activeInfo = panel.StageInfo;

                    updateOpacity(panel.GetIndex());
                };

            trackList.GetChild<StageSelectionPanel>(0).GrabFocus();

            startButton.Pressed += () =>
            {
                GetNode<AudioSource>("/root/GlobalAudio").SetTrack(activeInfo.TrackInfo);
                loadStage();
            };
        }

        // TODO : this should all be moved to its own class along with all the stage selection carousel stuff honestly.
        private void updateOpacity(int focusedIndex)
        {
            foreach (var panel in trackList.GetChildren().OfType<StageSelectionPanel>())
            {
                var alpha = panel.Modulate;
                alpha.A = 1f / (1 + Math.Abs(panel.GetIndex() - focusedIndex));

                opacityTween = CreateTween();
                opacityTween.TweenProperty(panel, "modulate", alpha, transition)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.Out);
            }
        }

        private void loadStage()
        {
            PlayerLoader player = new PlayerLoader(activeInfo);

            GetTree().Root.AddChild(player);
            GetTree().CurrentScene = player;
            GetTree().Root.RemoveChild(this);
        }
    }
}
