// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectableNote : Selectable
    {
        protected override Color HighlightColor => Colors.Blue;

        private CircleShape2D hitboxCircle = new CircleShape2D { Radius = 31 };

        public SelectableNote()
        {
            CollisionShape.Shape = hitboxCircle;
        }

        public override void _Draw()
        {
            base._Draw();
            DrawCircle(Position, 32, Colors.White);
            DrawArc
            (
                Position,
                31,
                Mathf.DegToRad(0),
                Mathf.DegToRad(360),
                30,
                Colors.White,
                2
            );
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } || !IsHeld) return;

            // TODO: make this account for the distance of the mouse selection location to the note center.
            GetParent<Node2D>().GlobalPosition = GetGlobalMousePosition();
            GetViewport().SetInputAsHandled();
        }
    }
}
