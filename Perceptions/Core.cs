// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Perceptions
{
    public partial class Core : Perception
    {
        private const int jump_velocity = -1900;

        public override void _PhysicsProcess(double delta)
        {
            if (!Movable) return;

            base._PhysicsProcess(delta);

            nucleus_collision();

            if (IsOnFloor())
                ground_movement(delta);
            else
                air_movement(delta);

            MoveAndSlide();
        }

        private void ground_movement(double delta)
        {
            grounded_rotation(delta);

            if (Input.IsActionPressed("main"))
                Velocity = new Vector2(Velocity.X, jump_velocity);
        }

        private void grounded_rotation(double delta)
        {
            float targetRotation = Mathf.Snapped(Body.RotationDegrees, 90);

            Body.RotationDegrees = (float)Mathf.Lerp(Body.RotationDegrees, targetRotation, 20 * delta);
        }

        private void air_movement(double delta)
        {

            Body.Rotate(Mathf.DegToRad(360 * (float)delta));

            Velocity = new Vector2(Velocity.X, Mathf.Min(1500, Velocity.Y + Gravity * (float)delta));
        }

        private void nucleus_collision()
        {
            Nucleus.Modulate = Nucleus.HasOverlappingBodies() ? Colors.Red : Colors.Green;
        }
    }
}
