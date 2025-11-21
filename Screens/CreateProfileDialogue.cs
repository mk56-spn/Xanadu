using System;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.DataStructure;
using XanaduProject.IO;
using XanaduProject.Screens.ScreenStructure;
using AnimatedHoverButton = XanaduProject.UiElements.AnimatedHoverButton;

namespace XanaduProject.Screens
{
    public partial class CreateProfileDialogue : SubScreen
    {
        private readonly LineEdit nameEdit;
        private readonly AnimatedHoverButton confirmButton;
        private readonly AnimatedHoverButton cancelButton;
        private readonly Action onConfirm;

        public CreateProfileDialogue(Action onConfirm)
        {
            this.onConfirm = onConfirm;

            var panel = new Panel { SelfModulate = new Color(0, 0, 0, 0.7f) };
            AddChild(panel);
            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var vbox = new VBoxContainer();
            AddChild(vbox);
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, margin: 100);

            var title = new Label
            {
                Text = "Create New Profile"
            };
            vbox.AddChild(title);

            nameEdit = new LineEdit { PlaceholderText = "Enter Profile Name" };
            vbox.AddChild(nameEdit);

            var hbox = new HBoxContainer();
            vbox.AddChild(hbox);

            confirmButton = new AnimatedHoverButton("Confirm");
            confirmButton.Pressed += OnConfirmPressed;
            hbox.AddChild(confirmButton);

            cancelButton = new AnimatedHoverButton("Cancel");
            cancelButton.Pressed += OnCancelPressed;
            hbox.AddChild(cancelButton);
        }

        private void OnConfirmPressed()
        {
            string profileName = nameEdit.Text.Trim();
            if (string.IsNullOrEmpty(profileName))
            {
                return;
            }

            string profileDirName = $"profile_{DateTime.Now.ToFileTimeUtc()}";
            var newProfile = new ProfileInfo
            {
                ProfileName = profileName,
                CreationDate = DateTime.Now,
                ProfilePath = SerializationUtils.PROFILES_DIR.PathJoin(profileDirName)
            };

            ProfilePersistence.SaveProfile(newProfile);

            onConfirm();
        }

        private void OnCancelPressed()
        {
            ScreenManager.RemoveSubscreen();
        }
    }
}
