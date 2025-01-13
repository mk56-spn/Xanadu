// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Rendering;

namespace XanaduProject.ECSComponents
{
    public readonly struct DirectionEcs(Direction direction) : IComponent, IUpdatable
    {
        [Composer("Note")] public readonly Direction Direction = direction;

        public void Update(ElementEcs element)
        {
            float angle = Direction switch
            {
                Direction.Left => -Mathf.Pi,
                Direction.Right => 0,
                Direction.Up => -Mathf.Pi / 2,
                Direction.Down => Mathf.Pi / 2,
                _ => throw new ArgumentOutOfRangeException()
            };

            RenderingServer.CanvasItemSetTransform(element.Canvas, element.Transform.RotatedLocal(angle));
        }


        public void UpdateCharacter(RenderCharacter renderCharacter, ElementEcs elementEcs, NoteEcs noteEcs)
        {

            if (noteEcs.CenterPlayer)
                renderCharacter.Position = elementEcs.Transform.Origin;

            switch (Direction)
            {
                case Direction.Left:
                    renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = -750});
                    break;
                case Direction.Up:
                    renderCharacter.SetVelocity(renderCharacter.Velocity with{ Y = -1200});
                    break;
                case Direction.Right:
                    renderCharacter.SetVelocity(renderCharacter.Velocity with{ X = 750});
                    break;
                case Direction.Down:
                    renderCharacter.SetVelocity(renderCharacter.Velocity with{ Y = 1500});
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum Direction
    {
        Left,
        Right,
        Up,
        Down
    }
}
