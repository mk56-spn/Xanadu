// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Perceptions.Components
{
    /// <summary>
    /// Rhythm handles orbit the player node, and can be attached to a note chain to set off a "Line".
    /// </summary>
    public partial class RhythmHandle : Node2D
    {
        /// <summary>
        /// The key this handle represents
        /// </summary>
        public string Key = string.Empty;

        /// <summary>
        /// Main colour for this handle.
        /// </summary>
        public Color Colour
        {
            get => colour;
            set
            {
                colour = value;
                colourDark = value.Darkened(0.3f);
            }
        }
        private Color colour;
        private Color colourDark;

        public RhythmHandle ()
        {
            ZIndex = 1;
        }

        public override void _Process(double delta)
        {
            base._PhysicsProcess(delta);
            Modulate = Modulate.Lerp(Input.IsActionPressed(Key) ? colour : colourDark, (float)(15 * delta));
        }
    }
}
