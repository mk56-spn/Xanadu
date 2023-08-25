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

        // Make sure notes are ordered w.r.t music
        private Note[] notes => GetChildren().OfType<Note>().OrderBy(n => n.PositionInTrack).ToArray();


        [Export]
        private Color connectorColour
        {
            get => connector.Modulate;
            set => connector.Modulate = value;
        }

        private Line2D connector = new Line2D { ZIndex = -1 };

        public event EventHandler<bool>? OnFinished;
        public override void _Ready()
        {
            base._Ready();

            AddChild(connector);

            connector.Points  = notes.Select(n => n.Position).ToArray();

            foreach (var note in notes)
            {
                note.OnActivated += () =>
                {
                    if (!notes.Any(n => n.IsValid))
                        OnFinished?.Invoke(this, true);
                };
            }

            OnFinished += (_, _) =>
                CreateTween()
                    .TweenProperty(this, "modulate", new Color(Modulate, 0), 0.3f );
        }

        private int noteIndex;

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            Note? note = notes.GetValue(noteIndex) as Note;

            if (!Input.IsActionJustPressed("R1")) return;

            note?.Activate();

            if (notes.GetUpperBound(0) > noteIndex)
                noteIndex++;
        }
    }
}
