// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.Composer.Composable.Notes;

namespace XanaduProject.Composer
{
    [Tool]
    public partial class NoteLink : Node2D
    {
        [Export]
        private Gradient connectorColour
        {
            get => connector.Gradient;
            set => connector.Gradient = value;
        }

        [Export]
        private string rhythmKey { get; set; } = null!;

        public event Action? OnFinished;

        // Make sure notes are ordered w.r.t music
        public Note[] Notes => GetChildren().OfType<Note>().OrderBy(n => n.PositionInTrack).ToArray();
        private Line2D connector = new Line2D { ZIndex = -1 };

        public override void _Ready()
        {
            base._Ready();

            // Fades out and then disposes of the object.
            OnFinished += () =>
            {
                var label = ResourceLoader.Load<PackedScene>("res://Composer/Notes/NoteLinkResult.tscn")
                    .Instantiate<Label>();
                Notes.Last().AddChild(label);
                CreateTween()
                    .TweenProperty(connector, "modulate", new Color(Modulate, 0), 0.3f )
                    .Finished += QueueFree;
            };

            AddChild(connector);

            connector.Points  = Notes.Select(n => n.Position).ToArray();

            foreach (var note in Notes)
            {
                note.OnStateChanged += (_, _) =>
                {
                    if (Notes.Any(n => n.State != Note.NoteState.Judged) == false)
                        OnFinished?.Invoke();
                };
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            if (!Input.IsActionJustPressed(rhythmKey)) return;

            Note? currentNote = Notes.FirstOrDefault(n => n.State == Note.NoteState.Active);
            currentNote?.RequestState(Note.NoteState.Judged);
        }
    }
}
