// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Buttons;
using XanaduProject.DataStructure;
using XanaduProject.IO;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Stage;

namespace XanaduProject.Screens.StageSelection
{
	public partial class StageSelection : ScreenWithFooter
	{
		private AnimatedHoverButton startButton = new("Start");
		private AnimatedHoverButton editButton = new("Edit");
        private StageInfo data = null!;
        private readonly ScoreDisplay scoreDisplay = new();

        public StageInfo Data
        {
            get => data;
            set
            {
                data = value;
                dataDisplay.Update(value);
                scoreDisplay.Update(value);
            }
        }


        private readonly InfoDisplayContainer dataDisplay = new();
        private readonly VBoxContainer header = new();

        public override void _Ready()
		{
            var profileContainer = new VBoxContainer();
            AddChild(profileContainer);
            profileContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, margin: 10);

            var profileLabel = new Label();
            profileLabel.Text = GameSettings.CurrentProfile != null ? $"Current Profile: {GameSettings.CurrentProfile.ProfileName}" : "No profile selected.";
            profileContainer.AddChild(profileLabel);

            AddChild(header);

            header.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide,LayoutPresetMode.KeepSize, margin: 5);
            header.AddChild(new Container() { CustomMinimumSize = new Vector2(40,40)});
            header.AddChild(dataDisplay);
            header.AddChild(scoreDisplay);

            AddButtonToFooter(startButton);
            AddButtonToFooter(editButton);

			AddChild(new StageSelectionCarousel(this));

			startButton.Pressed += () =>
			{
				ScreenManager.RequestChangeScreen(() =>
					new Player(StagePersistence.GetStage(Data)),
					TransitionType.Fade);
			};

			editButton.Pressed += () =>
			{
				ScreenManager.RequestChangeScreen(() =>
					new Stage.Masters.Composer.Composer(StagePersistence.GetStage(Data)));
			};
		}

        private partial class HeaderButtons : HBoxContainer
        {
            public HeaderButtons()
            {
                AddChild(new Button());
            }
        }
	}
}
