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

        protected event Action? OnPositionChanged;

        protected float Radius
        {
            get => hitboxCircle.Radius;
            set => hitboxCircle.Radius = value;
        }

        protected SelectableHandle()
        {
            CollisionShape.Shape = hitboxCircle;
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } || !IsHeld) return;

            GlobalPosition = GetGlobalMousePosition();
            OnPositionChanged?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
