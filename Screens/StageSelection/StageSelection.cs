// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Godot;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Stage;

namespace XanaduProject.Screens.StageSelection
{
	public partial class StageSelection : Screen
	{
		private Button startButton = new() { Text = "Start" };
		private Button editButton = new() { Text = "Edit" };

		private HBoxContainer buttons = new();

		public StageInfo Data;

		public override void _Ready()
		{
			AddChild(buttons);
			buttons.AddChild(startButton);
			buttons.AddChild(editButton);

			AddChild(new StageSelectionCarousel(this));

			startButton.Pressed += () =>
			{
				ScreenManager.RequestChangeScreen(() =>
					new Player(StagePersistence.GetStage(Data),
						TrackIndex.GetTrackInfo(Data.SongIndex)),
					TransitionType.Fade);
			};

			editButton.Pressed += () =>
			{
				ScreenManager.RequestChangeScreen(() =>
					new Stage.Masters.Composer.Composer(StagePersistence.GetStage(Data),
						TrackIndex.GetTrackInfo(Data.SongIndex)));
			};
		}
	}
}
