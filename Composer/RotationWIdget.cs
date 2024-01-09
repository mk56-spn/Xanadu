// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    public partial class RotationWidget (ComposerRenderMaster composerRenderMaster, ComposerEditWidget composerEditWidget) : Node2D
    {
        private const float  radius = 200;

        private bool pressed;

        public override void _Process(double delta) => QueueRedraw();
        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (pressed)
                updateRotation();

            float distance = aggregatePosition().DistanceTo(GetLocalMousePosition());

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
            float angle = aggregatePosition().AngleToPoint(GetLocalMousePosition());

            if (composerEditWidget.Target != null)
                composerRenderMaster.RotateElement(composerEditWidget.Target.Value, Mathf.RadToDeg(angle));
        }
        public override void _Draw() {
            DrawSetTransform(aggregatePosition(), Mathf.DegToRad(composerRenderMaster.Dictionary[composerEditWidget.Target!.Value].Element.Rotation));

            DrawCircle(new Vector2(200, 0), 15, ComposerRenderMaster.COMPOSER_ACCENT);

            DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 60, ComposerRenderMaster.COMPOSER_ACCENT with { A = 0.25f }, 10);
            DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 60, ComposerRenderMaster.COMPOSER_ACCENT, 3);
        }



        private Vector2 aggregatePosition()
        {
            Vector2 positionAggregate = new Vector2();

            foreach (var position in composerRenderMaster.GetSelectedAreasPositions())
                positionAggregate += position;

            positionAggregate /= composerRenderMaster.GetSelectedAreasPositions().Length;

            return positionAggregate + GetViewport().CanvasTransform.Origin;
        }
    }
}
