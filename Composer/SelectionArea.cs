// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    [GlobalClass]
    public partial class SelectionArea : Node2D
    {
        private (Vector2 position, Vector2 size) region;

        public SelectionArea ()
        {
            GD.Print("ctor");
        }

        public override void _Draw()
        {
            base._Draw();

            DrawRect(new Rect2(region.position, region.size), Colors.IndianRed, false, 2);
            DrawRect(new Rect2(region.position, region.size), Colors.IndianRed with { A = 0.3f });

            ZIndex = 2;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                    GD.Print("pressed");
                    Visible = true;
                    region.position = GetGlobalMousePosition() - GlobalPosition;
                    region.size = Vector2.Zero;
                    QueueRedraw();
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                    GD.Print("released");
                    Visible = false;
                    QueueRedraw();
                    break;

                case InputEventMouseMotion { ButtonMask: MouseButtonMask.Left }:
                    region.size = GetGlobalMousePosition() - GlobalPosition - region.position;
                    QueueRedraw();
                    break;
            }

        }
    }
}
