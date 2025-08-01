// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerMacros(Composer composer) : Node
    {
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true } key) return;

            if (key is { KeyLabel: Key.R })
                composer.Selected.Each(new RotateElements());


            var direction = key.KeyLabel switch
            {
                Key.Up => Vector2.Up,
                Key.Down => Vector2.Down,
                Key.Left => Vector2.Left,
                Key.Right => Vector2.Right,
                _ => Vector2.Zero
            };
            if (direction != Vector2.Zero)
                composer.Selected.Each(new MoveElements(direction));
        }

        private readonly struct MoveElements(Vector2 direction) : IEach<ElementEcs, SelectionEcs>
        {
            public void Execute(ref ElementEcs element, ref SelectionEcs _)
            {
                int i = Input.IsKeyPressed(Key.Shift) ? 32 : 64;

                var newPos = element.Transform with { Origin = element.Transform.Origin + direction * i };
                element.Transform = newPos;
            }
        }

        private readonly struct RotateElements : IEach<ElementEcs, SelectionEcs>
        {
            public void Execute(ref ElementEcs element, ref SelectionEcs c2)
            {
                element.Transform = element.Transform.RotatedLocal(Mathf.Pi / 2);
            }
        }
    }
}
