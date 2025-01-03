// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;

namespace XanaduProject.ECSComponents
{
	[ComponentKey(null)]
	public struct SelectionEcs(Rid area) : IIndexedComponent<Rid>, IUpdatable
	{
		[Ignore]
		public Rid Area = area;

		public Rid GetIndexedValue() => Area;


		public void Update(ElementEcs elementEcs)
		{
			PhysicsServer2D.AreaSetTransform(Area, elementEcs.Transform);
		}
	}
}
