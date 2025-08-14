// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;

namespace XanaduProject.ECSComponents
{
	public struct ElementEcs() : IComponent
	{
		public int Index = 0;
		public Transform2D Transform = Transform2D.Identity;

		[Ignore] public ulong Id => Canvas.Id;
		[Ignore] public Vector2 Vector2 => Transform.Origin;

		[Ignore] public Rid Canvas;
	}
}
