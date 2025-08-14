// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Serialization;
using XanaduProject.Stage;

namespace XanaduProject.Tests
{
	public partial class RenderTest : Screens.ScreenManager
	{

		[Export] private Type type = Type.Composer;

		private enum Type
		{
			Composer,
			Player
		}
		public override void _Ready()
		{
			Player player;
			if (type == Type.Player) {
				player = new Player(StageDeserializer.Deserialize("level1"),
					GD.Load<TrackInfo>("res://Resources/TestTrack.tres"));
			}
			else {
				player = new Stage.Masters.Composer.Composer(StageDeserializer.Deserialize("level1"),
					GD.Load<TrackInfo>("res://Resources/TestTrack.tres"));
			}

			AddChild(player);
		}
	}
}
