// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Physics
{
    public partial class PhysicsMaster : Node2D, IPhysicsMaster
    {
        public override void _EnterTree()=> World2D = GetWorld2D();
        public World2D World2D { get; private set; } = null!;
    }
}
