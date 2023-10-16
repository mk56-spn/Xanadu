// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.Composer.Composable.Notes;
using XanaduProject.Perceptions.Components;
using XanaduProject.Tools;

namespace XanaduProject.Composer
{
    public partial class NoteLink : Node2D
    {
        private int noteIndex;

        public event Action? OnFinished;

        // Make sure notes are ordered w.r.t music
        public Note[] OrderedNotes { get; private set; } = null!;
        private Line2D connector = null!;
        private RhythmHandle handle = null!;

        public RhythmLine Line { get; private set; } = RhythmLine.BLine;

        public override void _Ready()
        {
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 32 } });

            OrderedNotes = GetChildren().OfType<Note>().OrderBy(n => n.PositionInTrack).ToArray();
            AddChild(connector = new Line2D { Modulate = XanaduUtils.GetLineColour(Line), ShowBehindParent = true });

            SetProcessUnhandledInput(false);

            // Fades out and then disposes of the object.
            OnFinished += () =>
            {
                CreateTween()
                    .TweenProperty(connector, "modulate", new Color(Modulate, 0), 0.3f )
                    .Finished += QueueFree;
            };
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            QueueRedraw();

            var points = OrderedNotes.Select(n => n.Position).ToArray();

            if (connector.Points != points)
                connector.Points = points;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (!@event.IsActionPressed(XanaduUtils.GetLineInput(Line))) return;

            if (noteIndex == OrderedNotes.Length - 1)
            {
                OnFinished?.Invoke();
                SetProcessUnhandledInput(false);
            }


            OrderedNotes[noteIndex].RequestState(Note.NoteState.Judged);
            noteIndex++;

            QueueRedraw();
        }
    }
}
