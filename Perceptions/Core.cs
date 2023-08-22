// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

namespace XanaduProject.Perceptions
{
    public partial class Core : Perception
    {
        private const int jump_velocity = -1900;

        private Tween? rotationTween;

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            nucleus_collision();

            if (IsOnFloor())
                ground_movement();
            else
                air_movement(delta);

            MoveAndSlide();
        }

        private void ground_movement()
        {
            grounded_rotation();

            if (Input.IsActionPressed("main"))
                Velocity = new Vector2(Velocity.X, jump_velocity);
        }

        private void grounded_rotation()
        {
            float targetRotation = Mathf.Snapped(Body.RotationDegrees, 90);

            if (rotationTween != null || !(Math.Abs(Body.RotationDegrees - targetRotation) > 0.01)) return;

            rotationTween = CreateTween();
            rotationTween.TweenProperty(Body, "rotation_degrees", targetRotation, 0.1);
        }

        private void air_movement(double delta)
        {
            // Ensure animation towards any floor alignment is ended immediately to avoid it continuing whilst in the air
            rotationTween?.Kill();
            rotationTween = null;

            Body.Rotate(Mathf.DegToRad(360 * (float)delta));

            Velocity = new Vector2(Velocity.X, Mathf.Min(1500, Velocity.Y + Gravity * (float)delta));
        }

        private void nucleus_collision()
        {
            Nucleus.Modulate = Nucleus.HasOverlappingBodies() ? Colors.Red : Colors.Green;
        }
    }
}
