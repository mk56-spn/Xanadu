// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct DirectionEcs : IComponent
    {
        public Direction Direction;

        public DirectionEcs(Direction direction)
        {
            Direction = direction;
        }
    }
	public enum Direction
	{
		UpLeft,
		Left,
		UpRight,
		Right,
		Up,
		Down
	}
}
