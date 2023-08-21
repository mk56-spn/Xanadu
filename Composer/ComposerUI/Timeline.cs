// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.Screens;

namespace XanaduProject.Composer.ComposerUI
{
    [SuperNode(typeof(Dependent))]
    public partial class Timeline : ScrollContainer
    {

        public override partial void _Notification(int what);

        private const float height = 150;
        private const float separation_ratio = 500;

        private Container container = new Container();
        private Container markerContainer = new Container();

        [Dependency] private Stage stage => DependOn<Stage>();
        [Dependency] private TrackHandler trackHandler => DependOn<TrackHandler>();

        public void OnResolved()
        {
            AddChild(container);
            container.AddChild(markerContainer);
            container.AddChild(closestBar);

            container.CustomMinimumSize = new Vector2((float)trackHandler.TrackLength * separation_ratio, 150);

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
            ScrollHorizontal = (int)(container.CustomMinimumSize.X * (trackHandler.TrackPosition / trackHandler.TrackLength));

            updateLines();
        }

        private float lastClosestPosition;

        private void updateLines()
        {
            float snappedPosition = (float)Mathf.Snapped(ScrollHorizontal, trackHandler.SecondsPerBeat * separation_ratio);
            if (snappedPosition.Equals(lastClosestPosition)) return;

            closestBar.Position = new Vector2(snappedPosition, closestBar.Position.Y);
            lastClosestPosition = snappedPosition;

            foreach (var child in markerContainer.GetChildren())
            {
                child.Free();
            }


            int i = -4;
            while (i <= 4)
            {
                markerContainer.AddChild(new Line2D
                {
                    DefaultColor = Colors.DarkGray,
                    Width = 2,
                    Points = new []
                    {
                        new Vector2(0, 0),
                        new Vector2(0, 100)
                    },
                    Position = new Vector2((float)(snappedPosition + i * separation_ratio * trackHandler.TrackLength), 0)
                });
                i++;
            }
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
