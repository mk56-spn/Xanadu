// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;

namespace XanaduProject.ECSComponents
{
    [ComponentKey(null)]
    public struct SelectionEcs(Rid area) : IIndexedComponent<Rid>
    {
        [Ignore] public Rid Area = area;

        public Vector2 LastPosition;

        public Rid GetIndexedValue() => Area;

        public void SetTransform(Transform2D transform)
        {
            PhysicsServer2D.AreaSetTransform(Area, transform);
        }
    }
}
