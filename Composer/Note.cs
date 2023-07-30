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

        public void Activate()
        {
            hitBox.Monitorable = false;

            CreateTween().TweenProperty(this, "scale", Vector2.Zero, 0.1f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }
    }
}
