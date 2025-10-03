// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Utils;
using static Godot.Control.SizeFlags;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingLayout : VBoxContainer
    {
        private readonly Container poseViewer = PoseServices.GetViewer();

        private readonly HBoxContainer keyFrameButtons = new(){ SizeFlagsHorizontal = ShrinkCenter};

        private readonly AnimatedSlider timePositionSlider;
        public PoseAnimatingLayout()
        {
            timePositionSlider = new AnimatedSlider()
            {
                ConsiderNubWidthForPlacement = true,
                MaxValue = PoseAnimatingScreen.Info.Duration, Step = 0.001f, SizeFlagsHorizontal = ExpandFill
            };
            timePositionSlider.ValueChanged += value =>
            {
                PoseAnimatingScreen.Info.AnimationActive = AnimationState.Active;
                PoseAnimatingScreen.Info.AnimationPos = (float)value;
            };

            var snapButton = new AnimatedHoverButton("SNAP",25);

            snapButton.Pressed += () => EditorState.SnappedTracks = !EditorState.SnappedTracks;

            var playBackButton = new AnimatedHoverButton("▶", 15);
            playBackButton.Pressed += () => PoseAnimatingScreen.Info.AnimationActive = AnimationState.Playing;

            var pauseButton = new AnimatedHoverButton("||", 15);
            pauseButton.Pressed += () => PoseAnimatingScreen.Info.AnimationActive = AnimationState.Active;

            var stopButton = new AnimatedHoverButton("■", 15);
            stopButton.Pressed += () =>
            {
                PoseAnimatingScreen.Info.AnimationActive = AnimationState.Active;
                PoseAnimatingScreen.Info.AnimationPos = 0;
            };

            VBoxContainer animationModes = new();
            foreach (var mode in Enum.GetValues<AnimationMode>())
            {
                var b = new AnimatedHoverButton(mode.ToDisplayString(), 20);
                b.Pressed += () => PoseAnimatingScreen.Info.Mode = mode;
                animationModes.AddChild(b);
            }


            this.AddChildren([
                new PanelContainer(){ CustomMinimumSize = new Vector2(0,50)},
                new PanelContainer(){ CustomMinimumSize = new Vector2(0,50)}
                    .Child(new HBoxContainer()
                        .AddChildren(keyFrameButtons,snapButton )
                    ),

                new PanelContainer(){ SizeFlagsVertical = ExpandFill }
                    .AddChildren(
                        new CenterContainer() { SizeFlagsVertical = ExpandFill }
                        .Child(poseViewer.Child(new BoneInputHandler(poseViewer, keyFrameButtons))),
                        new VBoxContainer(){ SizeFlagsHorizontal = ShrinkEnd }.AddChildren(animationModes, createPoseBehaviourButton())
                    ),
                new HBoxContainer().AddChildren(new HBoxContainer().AddChildren(playBackButton, pauseButton, stopButton),timePositionSlider)
            ]);
        }

        private Node createPoseBehaviourButton()
        {

            ToggleButtonWithText pose = new ToggleButtonWithText("Default to pose");
            pose.Toggled += value => PoseAnimatingScreen.Info.DefaultToPose = value;

            ToggleButtonWithText button1 = new("Lerp end to beginning");
            button1.Toggled += value => PoseAnimatingScreen.Info.LerpEndToBeginning = value;

            ToggleButtonWithText button2 = new("Lerp from pose");
            button2.Toggled += value => PoseAnimatingScreen.Info.LerpBeginningFromPose = value;

            return new VBoxContainer().AddChildren(pose, button1, button2);
        }
        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }


        public override void _Process(double delta)
        {
            base._Process(delta);

            if (PoseAnimatingScreen.Info.AnimationActive == AnimationState.Playing)
                timePositionSlider.SetValueSilent(PoseAnimatingScreen.Info.AnimationPos);
        }
    }
}
