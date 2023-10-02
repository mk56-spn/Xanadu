// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.Composer.Selectables;
using XanaduProject.Screens;
using XanaduProject.Composer.Composable.Notes;

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

        public Timeline ()
        {
            ProcessMode = ProcessModeEnum.Always;
            MouseFilter = MouseFilterEnum.Pass;
        }

        public void OnResolved()
        {
            AddChild(container);
            container.AddChild(markerContainer);
            container.AddChild(closestBar);

            container.CustomMinimumSize = new Vector2((float)trackHandler.TrackLength * separation_ratio, 150);

            // Load in markers for the notes in the stage
            foreach (var note in stage.Notes)
                addTimelineNote(note);

            GetTree().NodeAdded += node =>
            {
                if (node is Note note)
                    addTimelineNote(note);
            };

            trackHandler.SongPositionChanged += position =>
            {
                ScrollHorizontal = (int)(container.CustomMinimumSize.X * (position / trackHandler.TrackLength));
                updateLines();
            };
            return;

            void addTimelineNote(Note note) =>
                container.AddChild(new TimelineNote(trackHandler, note)
                {
                    Position = new Vector2(note.PositionInTrack * separation_ratio, height / 2)
                });
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseButton mouseButton) return;

            Control parent = GetParent<Control>();

            if (new Rect2(Vector2.Zero, parent.Size).HasPoint(parent.GetLocalMousePosition()) && mouseButton.Pressed)
                GetViewport().SetInputAsHandled();
        }

        private float lastClosestPosition;

        private void updateLines()
        {
            float snappedPosition = (float)Mathf.Snapped(ScrollHorizontal, trackHandler.SecondsPerBeat * separation_ratio);
            if (snappedPosition.Equals(lastClosestPosition)) return;

            closestBar.Position = new Vector2(snappedPosition, closestBar.Position.Y);
            lastClosestPosition = snappedPosition;
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

        private partial class TimelineNote : SelectableHandle
        {
            protected override Color HighlightColor => Colors.White;

            private Note note;

            private bool noteSelected;

            public TimelineNote (TrackHandler handler, Note note)
            {
                this.note = note;
                OnDragged += () =>
                {
                    double snappedPosition = Mathf.Snapped(GetParent<Control>().GetLocalMousePosition().X, handler.SecondsPerBeat * separation_ratio);
                    Position = Position with { X = (float)snappedPosition };
                    note.PositionInTrack = Position.X / separation_ratio;
                    QueueRedraw();
                };

                note.GetChildren().OfType<Selectable>().First().SelectionStateChanged += state =>
                {
                    noteSelected = state;
                    QueueRedraw();
                };
            }

            public override void _Draw()
            {
                base._Draw();

                DrawString(
                    ThemeDB.FallbackFont,
                    new Vector2(0, 30), note.PositionInTrack.ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment.Right
                );

                if (!noteSelected) return;
                DrawArc(Vector2.Zero, 20, 0, 2 * Mathf.Pi, 20, Colors.Purple, width: 4);
            }
        }
    }
}
