// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
	public partial class RotationWidget (ComposerEditWidget composerEditWidget) : Node2D
	{
		private const float  radius = 200;

		private bool pressed;

		public override void _Process(double delta) => QueueRedraw();
		public override void _Input(InputEvent @event)
		{
			base._Input(@event);

			if (pressed)
				updateRotation();

			float distance =  aggregatePosition().DistanceTo(GetLocalMousePosition());

			if (composerEditWidget.Visible == false) return;

			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
				pressed = false;

			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ) return;

			float scaleFactor = (1F / GetViewport().GetCamera2D().Zoom.X);
			float scaledRadius = scaleFactor * radius;
			float width = scaleFactor * 10;
			GD.Print(distance);
			if (!(distance >  scaledRadius - width  && distance < scaledRadius + width)) return;

			pressed = true;
			GetViewport().SetInputAsHandled();
		}

		private void updateRotation()
		{
			float angle = aggregatePosition().AngleToPoint(GetGlobalMousePosition());

			if (composerEditWidget.Target == null) return;

			composerEditWidget.Target.SetRotation(Mathf.RadToDeg(angle));
		}
		public override void _Draw()
		{
			if (composerEditWidget.Target == null) return;
			DrawSetTransform(aggregatePosition(), Mathf.DegToRad(composerEditWidget.Target!.Element.Rotation), Vector2.One / GetViewport().GetCamera2D().Zoom);

			DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 60, ComposerRenderMaster.COMPOSER_ACCENT with { A = 0.25f }, 10);
			DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 60, ComposerRenderMaster.COMPOSER_ACCENT, 3);

			DrawCircle(new Vector2(200, 0), 15, ComposerRenderMaster.COMPOSER_ACCENT);
			DrawCircle(new Vector2(200, 0), 10, Colors.White with { A = 0.3f * (float)Mathf.Abs(Mathf.Sin(4 * Time.GetUnixTimeFromSystem())) });
		}

		private Vector2 aggregatePosition()
		{
			return composerEditWidget.Target != null ? composerEditWidget.Target.Element.Position : Vector2.Zero;
		}
	}
}
