using System;
using System.Globalization;
using System.Threading;
using Godot;
using MiniAudioEx;
using Ownaudio;
using Ownaudio.Sources;
using XanaduProject.Audio;
using XanaduProject.DataStructure;

namespace XanaduProject
{
	/// <summary>
	/// Simple scene-tester that plays an .ogg and shows how far it has progressed.
	/// </summary>
	public partial class GarbageTest : Node2D
	{
		private ColorRect smoothedRect = new()
			{ Color = Colors.Green, Size = new Vector2(50, 50)};
		private ColorRect rect = new()
			{ Color = Colors.Blue, Size = new Vector2(50, 50)};

		private Label bpm = new();

		private Clock clock = new(new TrackInfo
		{
			SongTitle = "Heavens's Fall",
			Track = "/home/dhs/Documents/Xanadu/Resources/Helblinde - Heaven_s Fall.ogg",
			TimingPoints = [(3, 20), (5, 200)]

		});


		private GarbageTest()
		{
		}
		public override void _Ready()
		{



			AddChild(rect);
			AddChild(smoothedRect);
		}

		private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			AudioContext.Deinitialize();
		}



		public override void _UnhandledInput(InputEvent @event)
		{
			base._UnhandledInput(@event);
			if (@event is InputEventKey { Keycode: Key.Space, Pressed: true })
			{
				clock.TogglePause();
			}
		}

		public override void _Process(double delta)
		{


			bpm.Text = clock.CurrentBpm.ToString(CultureInfo.InvariantCulture);
		}
	}
}
