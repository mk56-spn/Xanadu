// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Rendering;

namespace XanaduProject.ECSComponents
{
	public readonly struct DirectionEcs() : IComponent, IUpdatable
	{
		[Composer("Direction")] public readonly Direction Direction = Direction.Up;
		[Composer("Strength")] public readonly Strength Strength = Strength.Medium;

		public static readonly int BASE_STRENGTH = 750;

		public void Update(ElementEcs element)
		{
		}
	}

	public enum Strength
	{
		Weak,
		Medium,
		Strong
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
