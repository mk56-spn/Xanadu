// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;

namespace XanaduProject.Tests
{
    [GlobalClass]
    public partial class AudioTest : CenterContainer
    {
        private TrackHandler trackHandler = new TrackHandler();
        private ColorRect beatRect = new ColorRect { CustomMinimumSize = new Vector2(200, 200) };
        private VBoxContainer container = new VBoxContainer { CustomMinimumSize = new Vector2(100, 0) };
        private Label measureText = new Label();
        private ProgressBar progressBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 0) };

        public override void _Ready()
        {
            setup();

            trackHandler.OnBeat += (_, _) =>
            {
                GD.Print(trackHandler.TrackPosition);
                measureText.Text = $"{trackHandler.Measure}";
                var t = CreateTween();
                t.TweenProperty(beatRect, "modulate", trackHandler.Measure == 1 ? Colors.LightGreen : Colors.MediumPurple, 0);
                t.TweenProperty(beatRect, "modulate", Colors.Black, 0.3);
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            progressBar.Value = trackHandler.SongProgressPercentage;
        }

        private void setup()
        {

            AddChild(container);


            AddChild(trackHandler);
            trackHandler.SetTrack(ResourceLoader.Load<TrackInfo>("res://Resources/TestTrack.tres"));
            trackHandler.StartTrack();

            container.AddChild(beatRect);
            container.AddChild(progressBar);
            container.AddChild(measureText);
        }
    }
}
