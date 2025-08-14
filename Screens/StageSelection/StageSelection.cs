// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Godot;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.Serialization;
using XanaduProject.Stage;

namespace XanaduProject.Screens.StageSelection
{
	public partial class StageSelection : Screen
	{
		[Export] private Button startButton = null!;
		[Export] private Button editButton = null!;


		public string Level = null!;

		public override void _Ready()
		{
			AddChild(new StageSelectionCarousel(this));

			startButton.Pressed += ()=>
				ScreenManager.RequestChangeScreen(() => new Player(StageDeserializer
					.Deserialize("level1"), GD.Load<TrackInfo>("res://Resources/TestTrack.tres")), TransitionType.Fade);

			editButton.Pressed += () =>
				ScreenManager.RequestChangeScreen(()=> new Stage.Masters.Composer.Composer(StageDeserializer
					.Deserialize("level1"),GD.Load<TrackInfo>("res://Resources/TestTrack.tres")));
		}

		private void something(out Vector2 vec2, out Vector3 vec3)
		{
			vec2 = new Vector2(1, 2);
			vec3 = new Vector3(1, 2, 3);
		}

		private void loadScene(Node scene)
		{
			something(out var vec2, out var vec3);

			GetTree().Root.AddChild(scene);
			GetTree().CurrentScene = scene;
			GetTree().Root.RemoveChild(this);
		}
	}
}
