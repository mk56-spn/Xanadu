// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using XanaduProject.Tools;
using static Godot.PhysicsServer2D;

namespace XanaduProject.ECSComponents
{
    public struct BlockEcs : IComponent
    {
        [Ignore] public const int COLLISION_FLAG = 0b00000000_00000000_10000000_00001101;

        [Ignore] public Rid Body { get; set; }

        public void SetTransform(Transform2D transform)
        {
            BodySetShapeTransform(Body, 0, transform);
        }
    }
}
