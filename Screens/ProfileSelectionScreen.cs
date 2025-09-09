// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;
using XanaduProject.DataStructure;
using XanaduProject.GameDependencies;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public partial class ProfileSelectionScreen : MainScreen
    {
        private readonly VBoxContainer profileButtons;
        private readonly AnimatedHoverButton createProfileButton;
        private readonly ScreenManager screenManager;

        public ProfileSelectionScreen()
        {
            screenManager = DiProvider.Get<ScreenManager>();
            var title = new Label { Text = "Select a Profile" };
            AddChild(title);

            profileButtons = new VBoxContainer();
            AddChild(profileButtons);

            populateProfileButtons();

            createProfileButton = new AnimatedHoverButton("Create New Profile");
            createProfileButton.Pressed += OnCreateProfilePressed;
            AddChild(createProfileButton);

            Ready += () => profileButtons.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft, margin: 100);
        }

        private void populateProfileButtons()
        {
            foreach (var child in profileButtons.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var profile in ProfileIndex.Profiles)
            {
                var profileButton = new AnimatedHoverButton(profile.Value.ProfileName);
                profileButton.Pressed += () => OnProfileSelected(profile.Value);
                profileButtons.AddChild(profileButton);
            }
        }

        private void OnProfileSelected(ProfileInfo profileInfo)
        {
            GameSettings.CurrentProfile = profileInfo;
            screenManager.RequestChangeScreen(new MainMenu());
        }

        private void OnCreateProfilePressed()
        {
            var createProfileDialogue = new CreateProfileDialogue(() =>
            {
                ProfileIndex.BuildIndex();
                populateProfileButtons();
                screenManager.RemoveSubscreen();
            });
            createProfileDialogue.Visible = true;
            screenManager.ChangeSubScreen(createProfileDialogue);
        }
    }
}
