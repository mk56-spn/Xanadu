// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Serialization;

namespace XanaduProject.Rendering
{
	public partial class RenderMasterPlayer : Node2D
	{
		[Export] public Button Restart = null!;
		private RenderMaster renderMaster;



		public RenderMasterPlayer()
		{
			renderMaster= new RenderMaster(StageDeserializer.Deserialize("level1"), GD.Load<TrackInfo>("res://Resources/TestTrack.tres"));
			AddChild(renderMaster);
			renderMaster.AddChild(new PlayerCamera(renderMaster));
		}

		public override void _Input(InputEvent @event)
		{
			base._Input(@event);

			if (@event is not InputEventKey { KeyLabel: Key.R, Pressed: true }) return;
			renderMaster.Clock.Restart();
		}

		public override void _EnterTree()
		{
			base._EnterTree();

			Restart.Pressed += () =>
				renderMaster.Clock.Restart();
		}

		private partial class PlayerCamera( RenderMaster renderMaster) : Camera2D
		{
			public override void _Process(double delta)=>
				Position = Position.Lerp(renderMaster.RenderCharacter.Position, (float)(3f * delta));
		}
	}
}
