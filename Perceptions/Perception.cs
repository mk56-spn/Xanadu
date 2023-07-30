// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Perceptions
{
    public abstract partial class Perception : CharacterBody2D
    {
        private const int base_velocity = 700;
        protected int Gravity;

        [Export]
        protected Area2D NoteReceptor { get; set; } = null!;

        protected Perception()
        {
            var fetchGravity = ProjectSettings.GetSetting("physics/2d/default_gravity");
            Gravity = fetchGravity.AsInt32();

            Velocity = new Vector2(base_velocity, 0);
        }
    }
}
