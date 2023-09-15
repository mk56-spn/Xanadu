// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

namespace XanaduProject.Composer.Selectables
{
    /// <summary>
    /// A <see cref="Selectable"/> that takes a Vector 2 and updates it based on where the <see cref="Selectable"/> is positioned.
    /// </summary>
    public abstract partial class SelectableHandle : Selectable
    {
        private readonly CircleShape2D hitboxCircle = new CircleShape2D { Radius = 10 };

        protected event Action? OnDragged;

        protected float Radius
        {
            get => hitboxCircle.Radius;
            set => hitboxCircle.Radius = value;
        }

        /// <summary>
        /// Whether this selectable should have its position changed when dragged.
        /// </summary>
        protected bool MoveOnDrag { get; set; }

        protected SelectableHandle()
        {
            CollisionShape.Shape = hitboxCircle;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } || !IsHeld) return;

            if (MoveOnDrag)
                GlobalPosition = GetGlobalMousePosition();

            OnDragged?.Invoke();
            GetViewport().SetInputAsHandled();
        }

        public override void _Draw()
        {
            base._Draw();
            DrawCircle(Vector2.Zero, Radius + 1, Colors.White);
            DrawArc
            (
                Vector2.Zero,
                Radius,
                0,
                Mathf.Pi * 2,
                (int)(Radius * 0.5 + 5),
                Colors.White,
                2
            );
        }
    }
}
