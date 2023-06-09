// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Singletons;

namespace XanaduProject.Tests
{
    [GlobalClass]
    public partial class AudioTest : CenterContainer
    {
        private AudioSource audioSource = null!;
        private ColorRect beatRect = new ColorRect { CustomMinimumSize = new Vector2(200, 200) };
        private VBoxContainer container = new VBoxContainer { CustomMinimumSize = new Vector2(100, 0) };
        private Label measureText = new Label();
        private ProgressBar progressBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 0) };

        public override void _Ready()
        {
            setup();

            audioSource.OnNewBeat += (_, _) =>
            {

                measureText.Text = $"{audioSource.Measure}";
                var t = CreateTween();
                t.TweenProperty(beatRect, "modulate", audioSource.Measure == 1 ? Colors.LightGreen : Colors.MediumPurple, 0);
                t.TweenProperty(beatRect, "modulate", Colors.Black, 0.3);
            };

            audioSource.RequestPlay = true;
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (audioSource.Stream == null) return;

            progressBar.Value = audioSource.SongProgressPercentage();
        }

        private void setup()
        {
            audioSource = GetNode<AudioSource>("/root/GlobalAudio");
            audioSource.SetTrack(ResourceLoader.Load<TrackInfo>("res://Resources/TestTrack.tres"));

            AddChild(container);

            container.AddChild(beatRect);
            container.AddChild(progressBar);
            container.AddChild(measureText);
        }
    }
}
