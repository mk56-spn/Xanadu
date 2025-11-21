// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.DataStructure;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
	public struct NoteEcs(NoteType type = NoteType.Main) : IComponent
	{
		public static readonly int RADIUS = 32;

		public NoteType NoteType = type;

		public float TimingPoint;
		public RenderRid NoteCanvas = RenderRid.Create();
	}
}
