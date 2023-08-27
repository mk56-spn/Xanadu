// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.Composer.Notes;

namespace XanaduProject.Composer
{
    [Tool]
    public partial class NoteLink : Node2D
    {
        [Export]
        private Color connectorColour
        {
            get => connector.Modulate;
            set => connector.Modulate = value;
        }

        public event Action? OnFinished;

        // Make sure notes are ordered w.r.t music
        private Note[] notes => GetChildren().OfType<Note>().OrderBy(n => n.PositionInTrack).ToArray();
        private Line2D connector = new Line2D { ZIndex = -1 };

        public override void _Ready()
        {
            base._Ready();

            // Fades out and then disposes of the object.
            OnFinished += () => CreateTween()
                .TweenProperty(this, "modulate", new Color(Modulate, 0), 0.3f )
                .Finished += QueueFree;

            AddChild(connector);

            connector.Points  = notes.Select(n => n.Position).ToArray();

            foreach (var note in notes)
            {
                note.OnStateChanged += (_, _) =>
                {
                    if (notes.Any(n => n.State != Note.NoteState.Judged) == false)
                        OnFinished?.Invoke();
                };
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!Input.IsActionJustPressed("R1")) return;

            Note? currentNote = notes.FirstOrDefault(n => n.State == Note.NoteState.Active);
            currentNote?.RequestState(Note.NoteState.Judged);
        }
    }
}
