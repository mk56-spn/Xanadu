// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

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

        private RhythmHandle ()
        {
            ZIndex = 1;
        }

        public override void _Process(double delta)
        {
            base._PhysicsProcess(delta);
            Modulate = Modulate.Lerp(
                Input.IsActionPressed(GetLineInput(Line))
                    ? GetLineColour(Line)
                    : GetLineColour(Line).Darkened(0.2f), (float)(15 * delta));
        }

        public static RhythmHandle CreateHandle(RhythmLine line)
        {
            RhythmHandle handle = GD.Load<PackedScene>("res://Perceptions/Components/RhythmHandle.tscn")
                .Instantiate<RhythmHandle>();

            handle.Line = line;
            return handle;
        }
    }
}
