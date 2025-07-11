// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
    [ComponentKey(null)]
    public readonly struct
        HitZoneEcs(Rid area) : IIndexedComponent<Rid>, IUpdatable
    {
        public static readonly uint NOTE_AREA_FLAG = 0b00000000_00000000_10000000_00000000;

        public Rid GetIndexedValue()
        {
            return area;
        }

        public void Update(ElementEcs elementEcs)
        {
            AreaSetTransform(area, elementEcs.Transform);
        }
    }
}
