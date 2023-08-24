// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;

namespace XanaduProject.Composer
{
    [Tool]
    public partial class NoteLink : Node2D
    {
        private Line2D connector = new Line2D { ZIndex = -1 };
        private Note[] notes => GetChildren().OfType<Note>().ToArray();

        [Export]
        private Color connectorColour
        {
            get => connector.Modulate;
            set => connector.Modulate = value;
        }

        public event EventHandler<bool>? OnFinished;
        public override void _Ready()
        {
            base._Ready();

            AddChild(connector);

            // Makes sure the note list is ordered by song position before extracting positions to ensure connector goes through nodes in order;
            connector.Points  = notes.OrderBy(n => n.PositionInTrack).Select(n => n.Position).ToArray();

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
    }
}
