// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    public partial class PanningCamera : Camera2D
    {
        public PanningCamera ()
        {
            AnchorMode = AnchorModeEnum.FixedTopLeft;
        }
        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Middle } mouse) return;
            Offset -= mouse.Relative;

            GetViewport().SetInputAsHandled();
        }
    }
}
