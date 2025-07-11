// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using Godot;
using XanaduProject.Composer;
using XanaduProject.DataStructure;
using XanaduProject.Serialization;

namespace XanaduProject.Tests
{
	public partial class RenderTest : Control
	{
		[Export] private Button serializeButton = null!;
		[Export] private Button randomButton = null!;
		[Export] private Label fps = null!;
		[Export] private Label stageInfo = null!;
		[Export] private SpinBox spinBox = null!;

		private ComposerRenderMaster renderMaster = null!;

		public override void _Ready()
		{
			base._Ready();

			renderMaster = new ComposerRenderMaster(StageDeserializer.Deserialize("level1"), GD.Load<TrackInfo>("res://Resources/TestTrack.tres"));
			AddChild(renderMaster);

			serializeButton.Pressed += () => { StageSerializer.Serialize(renderMaster.EntityStore, "level1"); };
		}

		public override void _Process(double delta)
		{
			base._Process(delta);
			stageInfo.Modulate = Colors.Yellow;
			fps.Text = Engine.GetFramesPerSecond().ToString(CultureInfo.InvariantCulture);
		}
	}
}
