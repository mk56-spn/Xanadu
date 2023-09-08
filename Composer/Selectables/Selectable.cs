// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;

namespace XanaduProject.Composer.Selectables
{
    [SuperNode(typeof(Dependent))]
    public abstract partial class Selectable : Area2D
    {
        public override partial void _Notification(int what);

        /// <summary>
        /// The colours the object will take on when it is selected
        /// </summary>
        protected abstract Color HighlightColor { get; }

        private const float hovered_opacity = 0.5f;
        private const float opacity = 0.2f;

        private bool isHovered;
        private bool isSelected;

        public event Action<bool>? SelectionStateChanged;

        /// <summary>
        /// Whether the object is currently being held and as such valid for dragging actions
        /// </summary>
        protected bool IsHeld;

        protected CollisionShape2D CollisionShape = new CollisionShape2D();

        protected Selectable ()
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

            if (!isHovered)
            {
                if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    Selected(false);
                return;
            }

            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                    SelfModulate = HighlightColor with { A = SelfModulate.A };
                    IsHeld = true;
                    Selected(true);
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                    SelfModulate = Colors.White with { A = SelfModulate.A };
                    IsHeld = false;
                    break;
            }
            GetViewport().SetInputAsHandled();
        }

        public void Selected(bool select)
        {
            if (select.Equals(isSelected)) return;

            isSelected = select;

            GD.Print(GetInstanceId());
            SelectionStateChanged?.Invoke(isSelected);
        }
    }
}
