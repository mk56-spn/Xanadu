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
        protected override Color HighlightColor  => Colors.Gold;

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


        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } || !IsHeld) return;

            if (MoveOnDrag)
                GlobalPosition = GetTruePosition();

            OnDragged?.Invoke();
            GetViewport().SetInputAsHandled();
        }

        public override void _Draw()
        {
            base._Draw();
            DrawCircle(Vector2.Zero, Radius + 1, Colors.White with { A = 0.5f });
            DrawArc
            (
                Vector2.Zero,
                Radius,
                0,
                Mathf.Pi * 2,
                20,
                Colors.White,
                2,
                antialiased: true
            );
        }
    }
}
