// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
	public partial class PanningCamera : Camera2D
	{
		// Currently, we zoom towards the screen centre, which is not ideal.
		private static readonly Vector2 min_zoom = new(0.5f, 0.5f);
		private static readonly Vector2 max_zoom = new(4f, 4f);

		public override void _Ready() =>
			PositionSmoothingEnabled = true;

		private Vector2 mousepos;

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

			mousepos = GetGlobalMousePosition();

			if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Middle } mouse) return;
			Offset -= mouse.ScreenRelative / Zoom;

			GetViewport().SetInputAsHandled();
		}
	}
}
