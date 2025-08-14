// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents
{
	public struct RectEcs(Vector2 extents) : IComponent
	{
		public Vector2 Extents = extents;
	}
}
