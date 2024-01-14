// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    public partial class PanningCamera : Camera2D
    {
        // Currently we zoom towards the screen center which is not ideal.
        private static readonly Vector2 min_zoom = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 max_zoom = new Vector2(4f, 4f);

        public override void _Ready()
        {
            base._Ready();

            PositionSmoothingEnabled = true;
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is InputEventMouseButton button)
                switch (button.ButtonIndex)
                {
                    case MouseButton.WheelDown:
                        Zoom = (Zoom -= Zoom / 30).Clamp(min_zoom, max_zoom);
                        break;
                    case MouseButton.WheelUp:
                        Zoom = (Zoom += Zoom / 30).Clamp(min_zoom, max_zoom);
                        break;
                    default:
                        return;
                }

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Middle } mouse) return;
            Offset -= mouse.Relative;

            GetViewport().SetInputAsHandled();
        }
    }
}
