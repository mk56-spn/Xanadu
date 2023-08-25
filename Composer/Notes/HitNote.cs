// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Notes
{
    /// <summary>
    /// A note with a HitBox.
    /// </summary>
    public partial class HitNote : Note
    {
        [Export]
        private Area2D hitBox { get; set; } = null!;

        public override void _Ready()
        {
            base._Ready();

            OnActivated += () => hitBox.Monitorable = false;
        }
    }
}
