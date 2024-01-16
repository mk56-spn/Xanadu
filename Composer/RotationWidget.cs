// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    public partial class RotationWidget (ComposerEditWidget composerEditWidget) : Node2D
    {
        private const float  radius = 200;

        private bool pressed;


        private Vector2 truePosition =>  aggregatePosition() * GetViewport().CanvasTransform.Scale + GetViewport().CanvasTransform.Origin;

        public override void _Process(double delta) => QueueRedraw();
        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (pressed)
                updateRotation();

            float distance = truePosition.DistanceTo(GetLocalMousePosition());

            if (GetParent<Control>().Visible == false) return;

            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
                pressed = false;

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ) return;
            if (distance is <= radius - 10 or >= radius + 10) return;

            pressed = true;
            GetViewport().SetInputAsHandled();
        }

        private void updateRotation()
        {
            float angle = truePosition.AngleToPoint(GetLocalMousePosition());

            if (composerEditWidget.Target == null) return;

            composerEditWidget.Target.SetRotation(Mathf.RadToDeg(angle));
        }
        public override void _Draw()
        {

            DrawSetTransform(truePosition, Mathf.DegToRad(composerEditWidget.Target!.Element.Rotation));

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
