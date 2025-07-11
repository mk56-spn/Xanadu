// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents
{
	public struct RectEcs(Vector2 extents) : IComponent
	{
		public Vector2 Extents = extents;

		public static readonly Vector2[] PRESETS =
		[
			new(32, 32),
			new(64, 64),
			new(128, 128),
			new(128, 32),
			new(256, 256),
			new(10000, 10000),
		];
	}
}
