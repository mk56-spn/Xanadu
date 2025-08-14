// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents
{
	public readonly struct DirectionEcs(Direction direction) : IComponent
	{
		[Composer("Direction")] public readonly Direction Direction = direction;
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
