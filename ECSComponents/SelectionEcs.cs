// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
    [ComponentKey(null)]
    public struct SelectionEcs(Rid area) : IIndexedComponent<Rid>, IUpdatable
    {
        [Ignore] public Rid Area = area;

        public Rid GetIndexedValue()
        {
            return Area;
        }

        public void Update(ElementEcs elementEcs)
        {
            AreaSetTransform(Area, elementEcs.Transform);
        }
    }
}
