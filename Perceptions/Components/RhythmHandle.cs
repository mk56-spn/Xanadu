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
        /// The rhythm channel this handle represents.
        /// </summary>
        public readonly RhythmChannel Channel = new RhythmChannel();

        public RhythmInstance Instance
        {
            get => Channel.Instance;
            set => Channel.Instance = value;
        }

        public RhythmHandle ()
        {
            ZIndex = 1;
        }

        public override void _Process(double delta)
        {
            base._PhysicsProcess(delta);
            Modulate = Modulate.Lerp(
                Input.IsActionPressed(Channel.GetRhythmInput())
                    ? Channel.GetRhythmColour()
                    : Channel.GetRhythmColour().Darkened(0.2f), (float)(15 * delta));
        }
    }
}
