// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;

namespace XanaduProject.ECSComponents
{
    public struct AreaEcs : IIndexedComponent<Rid>
    {
        [Ignore] public Rid Area;
        public Rid GetIndexedValue() => Area;

        public void SetTransform(Transform2D transform)
        {
            PhysicsServer2D.AreaSetShapeTransform(Area, 0, transform);
        }
    }
}
