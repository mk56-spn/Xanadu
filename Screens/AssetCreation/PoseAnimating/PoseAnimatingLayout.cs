// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Utils;
using static Godot.Control.SizeFlags;
using AnimatedHoverButton = XanaduProject.UiElements.AnimatedHoverButton;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.IO.Indexes;
using XanaduProject.IO.Indexes.XanaduProject.IO;
using XanaduProject.Singleton;
using XanaduProject.UiElements;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class PoseAnimatingLayout : VBoxContainer
    {
        private readonly Container poseViewer = PoseServices.GetViewer();

        private readonly HBoxContainer keyFrameButtons = new(){ SizeFlagsHorizontal = ShrinkCenter};

        private readonly AnimatedSlider timePositionSlider;
        private PanelContainer? renamePopup;
        private LineEdit? renameLineEdit;
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

            var pauseButton = new AnimatedHoverButton("| |", 15);
            pauseButton.Pressed += () => PoseAnimatingScreen.Info.AnimationActive = AnimationState.Active;

            var stopButton = new AnimatedHoverButton("■", 15);
            stopButton.Pressed += () =>
            {
                PoseAnimatingScreen.Info.AnimationActive = AnimationState.Active;
                PoseAnimatingScreen.Info.AnimationPos = 0;
            };

            var saveButton = new AnimatedHoverButton("Save", 25);
            saveButton.Pressed += OnSavePressed;

            var renameButton = new AnimatedHoverButton("Rename", 25);
            renameButton.Pressed += OnRenamePressed;

            var duration = new AnimatedSlider() { Step = 0.01f , CustomMinimumSize = new Vector2( 100, 20), ConsiderNubWidthForPlacement = true };
            duration.ValueChanged += value =>
            {
                PoseAnimatingScreen.Info.Duration = (float)value;
                timePositionSlider.MaxValue = (float)value;
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
                        .AddChildren(keyFrameButtons,snapButton, new AnimatedExpandableDropdown("IO", (Container)new HBoxContainer().AddChildren( saveButton, renameButton, duration)))),

                new PanelContainer(){ SizeFlagsVertical = ExpandFill, ZIndex = -100 }
                    .AddChildren(
                        new CenterContainer() { SizeFlagsVertical = ExpandFill }
                        .Child(poseViewer.Child(new BoneInputHandler(poseViewer, keyFrameButtons))),
                        new VBoxContainer(){ SizeFlagsHorizontal = ShrinkEnd }.AddChildren(animationModes, createPoseBehaviourButton())
                    ),
                new HBoxContainer().AddChildren(new HBoxContainer().AddChildren(playBackButton, pauseButton, stopButton),timePositionSlider)
            ]);
            RenderRid.Create(poseViewer)
                .AddLine(new Vector2(-100, 140), new Vector2(100, 140), Colors.Red);
        }

        private void OnSavePressed()
        {
            Logger.AddLog(LogCategory.General,"Saving animation...");
            var screen = GetParent<PoseAnimatingScreen>();

            if (screen.AnimationPath != null)
            {
                AnimationIo.SaveAnimation(GameServices.Store, screen.AnimationPath);
                AnimationIndex.BuildIndex();
                return;
            }

            const string animation_name = "NewAnimation";

            int i = 1;
            string finalPath = animation_name + SerializationUtils.ANIMATIONS_EXT;
            while (File.Exists(Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.ANIMATIONS_DIR), finalPath)))
            {
                finalPath = $"{animation_name}{i++}{SerializationUtils.ANIMATIONS_EXT}";
            }

            screen.AnimationPath = finalPath;
            AnimationIo.SaveAnimation(GameServices.Store, finalPath);
            AnimationIndex.BuildIndex();
        }

        private void OnRenamePressed()
        {
            var screen = GetParent<PoseAnimatingScreen>();

            if (screen.AnimationPath == null)
            {
                return;
            }

            if (renamePopup == null)
            {
                createRenamePopup();
            }

            if (renamePopup == null || renameLineEdit == null) return;
            renameLineEdit.Text = Path.GetFileNameWithoutExtension(screen.AnimationPath);
            renamePopup.Visible = true;
            renameLineEdit.GrabFocus();
        }

        private void createRenamePopup()
        {
            renamePopup = new PanelContainer
            {
                Position = new Vector2(-300, 100),
                CustomMinimumSize = new Vector2(300, 150)
            };

            var vbox = new VBoxContainer();
            renamePopup.AddChild(vbox);

            var title = new Label { Text = "Rename Animation" };
            vbox.AddChild(title);

            renameLineEdit = new LineEdit { PlaceholderText = "Enter new name" };
            vbox.AddChild(renameLineEdit);

            var hbox = new HBoxContainer();
            vbox.AddChild(hbox);

            var confirmButton = new AnimatedHoverButton("Confirm", 20);
            confirmButton.Pressed += OnRenameConfirmed;
            hbox.AddChild(confirmButton);

            var cancelButton = new AnimatedHoverButton("Cancel", 20);
            cancelButton.Pressed += OnRenameCancelled;
            hbox.AddChild(cancelButton);

            AddChild(renamePopup);
            renamePopup.Visible = false;
        }

        private void OnRenameConfirmed()
        {
            if (renameLineEdit == null || renamePopup == null)
                return;

            string newName = renameLineEdit.Text.Trim();
            if (string.IsNullOrEmpty(newName))
                return;

            var screen = GetParent<PoseAnimatingScreen>();
            if (screen.AnimationPath == null)
                return;

            string? newPath = AnimationIndex.RenameAnimation(screen.AnimationPath, newName);

            if (newPath != null)
            {
                screen.AnimationPath = newPath;
            }

            renamePopup.Visible = false;
        }

        private void OnRenameCancelled()
        {
            if (renamePopup != null)
            {
                renamePopup.Visible = false;
            }
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
