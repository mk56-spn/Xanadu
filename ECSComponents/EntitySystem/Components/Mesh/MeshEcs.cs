// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
using Friflo.Engine.ECS;

namespace XanaduProject.ECSComponents.EntitySystem.Components.Mesh
{
    public struct MeshEcs : IComponent
    {
        public Vector2 Size;
        public float Radius;
        public float Inset;
    }
}
