// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Composer;
using static XanaduProject.Tools.XanaduUtils;

namespace XanaduProject.Perceptions.Components
{
    /// <summary>
    /// Rhythm handles orbit the player node, and can be attached to a note chain to set off a "Line".
    /// </summary>
    public partial class RhythmHandle : Control
    {
        /// <summary>
        /// Which of the three possible lines this corresponds to.
        /// </summary>
        public RhythmLine Line { get; private set; }

        private NoteLink? owner;
        private List<NoteLink> noteLinks = null!;

        private RhythmHandle ()
        {
            ZIndex = 1;
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event.IsActionPressed(GetLineInput(Line)) && owner is null)
                addOwner();
        }

        public override void _Process(double delta)
        {
            base._PhysicsProcess(delta);
            Modulate = Modulate.Lerp(
                Input.IsActionPressed(GetLineInput(Line))
                    ? GetLineColour(Line)
                    : GetLineColour(Line).Darkened(0.2f), (float)(15 * delta));
        }

        private void addOwner()
        {
            if (!noteLinks.Any()) return;

            owner = noteLinks.First();
            noteLinks.Remove(owner);

            owner.SetProcessUnhandledInput(true);
            owner.OnFinished += () => owner = null;
        }

        public static RhythmHandle CreateHandle(RhythmLine line, List<NoteLink> noteLinks)
        {
            RhythmHandle handle = GD.Load<PackedScene>("res://Perceptions/Components/RhythmHandle.tscn")
                .Instantiate<RhythmHandle>();

            handle.noteLinks = noteLinks.Where(n => n.Line == line).ToList();
            handle.Line = line;
            return handle;
        }
    }
}
