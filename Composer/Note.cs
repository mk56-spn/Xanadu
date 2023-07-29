// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    [GlobalClass]
    public partial class Note : Node2D
    {
        [Export]
        private Polygon2D body { get; set; } = null!;

        [Export]
        private Area2D hitBox { get; set; } = null!;

        public bool IsHit { private set; get; }

        public void Activate()
        {
            IsHit = true;
            hitBox.Monitorable = false;

            body.Color = Colors.Orange;
        }
    }
}
