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

        [Dependency] private bool snapped => DependOn<bool>();

        /// <summary>
        /// The colours the object will take on when it is selected
        /// </summary>
        protected virtual Color HighlightColor => Colors.White;

        protected bool IsSelected { get; private set; }

        /// <summary>
        /// The distance from the center of the selectable to the location at which you held it from.
        /// </summary>
        private Vector2 holdOffset;

        /// <summary>
        /// Update when the <see cref="Selectable"/> is (de)selected;
        /// </summary>
        public event Action<bool>? SelectionStateChanged;

        /// <summary>
        /// Whether the object is currently being held and as such valid for dragging actions
        /// </summary>
        protected bool IsHeld { get; private set; }

        private bool isHovered;

        protected CollisionShape2D CollisionShape = new CollisionShape2D();

        protected Selectable ()
        {
            ZIndex = 4;

            AddChild(CollisionShape);

            MouseEntered += () => isHovered = true;
            MouseExited += () => isHovered = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (CollisionShape.Shape == null) return;

            updateVisuals();

            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton) return;

            if (mouseButton.Pressed)
                Selected(isHovered);

            bool newHeldValue = isHovered && mouseButton.Pressed;

            if (!IsHeld.Equals(newHeldValue))
                holdOffset = GetGlobalMousePosition() - GlobalPosition;

            IsHeld = newHeldValue;

            if (!IsHeld) return;

            GetViewport().SetInputAsHandled();
        }

        private void updateVisuals() =>
            SelfModulate = IsHeld ? Colors.White : HighlightColor;

        public void Selected(bool select)
        {
            if (select.Equals(IsSelected)) return;

            IsSelected = select;
            SelectionStateChanged?.Invoke(IsSelected);
        }

        /// <summary>
        /// Returns position accounting for whether snap is enabled;
        /// </summary>
        /// <returns></returns>
        protected Vector2 GetTruePosition(bool withOffset = false)
        {
            Vector2 pos = GetGlobalMousePosition();

            pos = withOffset ? pos - holdOffset : pos;

            return !snapped ? pos : pos.Snapped(new Vector2(Composer.GRID_SIZE, Composer.GRID_SIZE));
        }
    }
}
