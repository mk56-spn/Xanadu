// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer.Selectables
{
    public abstract partial class Selection : Area2D
    {
        /// <summary>
        /// The colours the object will take on when it is selected
        /// </summary>
        protected abstract Color HighlightColor { get; }

        private const float hovered_opacity = 0.5f;
        private const float opacity = 0.2f;

        private bool isHovered;

        /// <summary>
        /// Whether the object is currently being held and as such valid for dragging actions
        /// </summary>
        protected bool IsHeld;

        protected CollisionShape2D CollisionShape = new CollisionShape2D();

        protected Selection ()
        {
            AddChild(CollisionShape);

            SelfModulate = SelfModulate with { A = opacity };

            MouseEntered += () =>
            {
                SelfModulate = SelfModulate with { A = hovered_opacity };
                isHovered = true;
            };
            MouseExited += () =>
            {
                SelfModulate = SelfModulate with { A = opacity };
                isHovered = false;
            };
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (!isHovered) return;

            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                    SelfModulate = HighlightColor with { A = SelfModulate.A };
                    IsHeld = true;
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                    SelfModulate = Colors.White with { A = SelfModulate.A };
                    IsHeld = false;
                    break;
            }
        }
    }
}
