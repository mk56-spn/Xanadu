// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens;
using XanaduProject.Singletons;

namespace XanaduProject.Composer.ComposerUI
{
    [SuperNode(typeof(Dependent))]
    public partial class Timeline : ScrollContainer
    {
        public override partial void _Notification(int what);

        private const float height = 150;
        private const float separation_ratio = 500;

        private Container container = new Container();
        private AudioSource audioSource = SingletonSource.GetAudioSource();
        private Container markerContainer = new Container();

        [Dependency] private Stage stage => DependOn<Stage>();

        public void OnResolved()
        {
            AddChild(container);
            container.AddChild(markerContainer);
            markerContainer.AddChild(closestBar);

            container.CustomMinimumSize = new Vector2((float)audioSource.Stream.GetLength() * separation_ratio, 150);

            GD.Print($"Stage has {stage.Notes.Count()} notes");

            // Load in markers for the notes in the stage
            foreach (var note in stage.Notes)
            {
                container.AddChild(new Line2D
                {
                    Position = new Vector2(note.PositionInTrack * separation_ratio, 0),
                    Points = new []
                    {
                        new Vector2(0, 30),
                        new Vector2(0, height - 30),
                    },
                    DefaultColor = Colors.Cyan,
                    Width = 2,
                    ZIndex = 1
                });
            }
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            ScrollHorizontal = (int)(container.CustomMinimumSize.X * (audioSource.TrackPosition / audioSource.Stream.GetLength()));

            updateLines();
        }

        private void updateLines()
        {
            float snappedPosition = (float)Mathf.Snapped(ScrollHorizontal, audioSource.SecondsPerBeat * separation_ratio);
            closestBar.Position = new Vector2(snappedPosition, closestBar.Position.Y);
        }

        private Line2D closestBar = new Line2D
        {
            Width = 2,
            DefaultColor = Colors.Red,
            Position = new Vector2(0, 10),
            Points = new []
            {
                new Vector2(0, 0),
                new Vector2(0, 30)
            }
        };
    }
}
