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
        protected virtual Color HighlightColor => Colors.White;

        private const float hovered_opacity = 0.5f;
        private const float opacity = 0.2f;

        private bool isSelected;

        public event Action<bool>? SelectionStateChanged;

        /// <summary>
        /// Whether the object is currently being held and as such valid for dragging actions
        /// </summary>
        protected bool IsHeld { get; private set; }
        protected bool IsHovered { get; private set; }

        protected CollisionShape2D CollisionShape = new CollisionShape2D();

        protected Selectable ()
        {
            AddChild(CollisionShape);

            SelfModulate = SelfModulate with { A = opacity };

            MouseEntered += () => IsHovered = true;
            MouseExited += () => IsHovered = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);
            updateVisuals();

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton) return;

            if (mouseButton.Pressed)
                Selected(IsHovered);

            IsHeld = IsHovered && mouseButton.Pressed;

            if (IsHovered && mouseButton.Pressed)
                GetViewport().SetInputAsHandled();
        }

        private void updateVisuals()
        {
            float alpha = IsHovered ? opacity : hovered_opacity;
            Color color = IsHeld ? Colors.White : HighlightColor;

            SelfModulate = color with { A = alpha };
        }

        public void Selected(bool select)
        {
            if (select.Equals(isSelected)) return;

            isSelected = select;
            SelectionStateChanged?.Invoke(isSelected);
        }
    }
}
