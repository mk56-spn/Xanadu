// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using static Godot.Mathf;

namespace XanaduProject.Tests
{
	[GlobalClass]
	public partial class AudioTest : CenterContainer
	{
		private TrackHandler trackHandler;
		private ColorRect beatRect = new() { CustomMinimumSize = new Vector2(200, 200) };
		private VBoxContainer container = new() { CustomMinimumSize = new Vector2(100, 0) };
		private Label measureText = new();
		private ProgressBar progressBar = new() { CustomMinimumSize = new Vector2(200, 0) };
		public AudioTest()
		{
			setup();
			trackHandler!.OnBeat += (_, _) =>
			{
				GD.Print(trackHandler.TrackPosition);
				measureText.Text = $"{trackHandler.Measure}";
				var t = CreateTween();

				t.TweenProperty(beatRect, "modulate", Colors.Black, 0.3f).
					From(trackHandler.Measure == 1 ? Colors.LightGreen : Colors.MediumPurple);
				t.TweenProperty(beatRect, "scale", new Vector2(1,1), 0.3f).From(new Vector2(2,2));
			};

		}

		public override void _Process(double delta)
		{
			base._Process(delta);

			progressBar.Value = trackHandler.SongProgressPercentage;
			beatRect.PivotOffset = beatRect.Size / 2;
		}

		private void setup()
		{

			AddChild(container);

			trackHandler = new TrackHandler(ResourceLoader.Load<TrackInfo>("res://Resources/TestTrack.tres"));
			AddChild(trackHandler);

			container.AddChild(beatRect);
			container.AddChild(progressBar);
			container.AddChild(measureText);
			AddChild(new Waveform(trackHandler));

			Ready += () => trackHandler.StartTrack();
		}

		public override void _Draw()
		{
			base._Draw();

			DrawCircle(new Vector2(0, 1000), 5, Colors.Aquamarine);
		}
		private partial class Waveform2(TrackHandler trackHandler) : Node2D
		{
			private const int rate = 44;

			public override void _EnterTree()
			{
				Position = new Vector2(0, 1000);
				base._EnterTree();
			}

			public override void _Process(double delta)
			{
				base._Process(delta);

				Position = Position with { X = -(float)(trackHandler.TrackPosition * (44100f / rate))};
			}

			public override void _Draw()
			{
				GD.Print(trackHandler.Buffer.Length);
				if (trackHandler.Buffer.Length == 0) return;

				Vector2[] buffer = trackHandler.Buffer;
				Vector2[] pointsLeft = new Vector2[buffer.Length];
				Vector2[] pointsRight = new Vector2[buffer.Length];

				for (int i = 0; i < buffer.Length; i++)
				{
					pointsLeft[i] = new Vector2(i , Abs(buffer[i].X  * 200));
					pointsRight[i] = new Vector2(i , -Abs(buffer[i].Y) * 200);
				}

				DrawPolyline(pointsRight,Colors.Turquoise);
				DrawPolyline(pointsLeft,Colors.HotPink);
			}
		}

		private partial class Waveform(TrackHandler trackHandler) : Node2D
		{
			private const int rate = 44;

			public override void _EnterTree()
			{
				Position = new Vector2(0, 1000);
				base._EnterTree();


				trackHandler.OnSongCommence += () => QueueRedraw();
				SpinBox spin = new SpinBox();
				spin.Editable = true;

				GetParent().Ready += () => GetParent().AddChild(spin);
				spin.Step = 0.001;
				spin.MaxValue = 1;
				spin.MinValue = 0;
				spin.Value = 0.05;

				spin.ValueChanged += value =>
				{
					offset = (float)value;
					QueueRedraw();
				};
			}

			private float offset;

			public override void _Process(double delta)
			{
				base._Process(delta);

				Position = Position with { X = -(float)(trackHandler.TrackPosition * (44100f / rate))};
			}

			public override void _Draw()
			{
				GD.Print(trackHandler.Buffer.Length);
				if (trackHandler.Buffer.Length == 0) return;

				Vector2[] buffer = trackHandler.Buffer;
				Vector2[] pointsLeft = new Vector2[buffer.Length];
				Vector2[] pointsRight = new Vector2[buffer.Length];

				for (int i = 0; i < buffer.Length; i++)
				{
					pointsLeft[i] = new Vector2(i , Abs(buffer[i].X  * 200));
					pointsRight[i] = new Vector2(i , -Abs(buffer[i].Y) * 200);
				}

				DrawPolyline(pointsRight,Colors.Turquoise);
				DrawPolyline(pointsLeft,Colors.HotPink);

				var point = new Vector2[2000];

				const float audio_rate = 44100 / 44f;
				double bpm = 60 / trackHandler.Bpm;
				float spacing = (float)(audio_rate * bpm);
				float trueOffset = offset * audio_rate;

				for (int i = 0; i < 2000; i += 2)
				{
					point[i] = new Vector2(spacing * i + trueOffset, -50);
					point[i + 1] = new Vector2(spacing * i + trueOffset, 50);
				}
				DrawMultiline(point, Colors.Yellow, -1);
			}
		}
	}
}
