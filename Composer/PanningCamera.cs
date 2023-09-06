// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    public partial class PanningCamera : Camera2D
    {
        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (!Input.IsKeyPressed(Key.Space)) return;
            if (@event is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouse)
                Offset -= mouse.Relative;
        }
    }
}
