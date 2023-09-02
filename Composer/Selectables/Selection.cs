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

        protected CollisionShape2D CollisionShape = new CollisionShape2D();

        public Selection ()
        {
            AddChild(CollisionShape);

            Modulate = Modulate with { A = opacity };

            MouseEntered += () =>
            {
                Modulate = Modulate with { A = hovered_opacity };
                isHovered = true;
            };
            MouseExited += () =>
            {
                Modulate = Modulate with { A = opacity };
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
                    Modulate = HighlightColor with { A = Modulate.A };
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                    Modulate = Colors.White with { A = Modulate.A };
                    break;
            }
        }
    }
}
