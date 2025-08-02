// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Character
{
    public partial class CharacterDamage : Node2D
    {
        public override void _PhysicsProcess(double delta)
        {
            if (queryDamage())
                DiProvider.Get<IStageConductor>().DamageTaken();
        }

        private bool queryDamage()
        {
            var query = new PhysicsShapeQueryParameters2D
            {
                Transform = GlobalTransform,
                Shape = new CircleShape2D { Radius = 32 },
                CollideWithAreas = true,
                CollisionMask = 0b_00000000_10000000_00000000_00000000
            };

            return GetWorld2D().DirectSpaceState
                .IntersectShape(query).Count != 0;
        }
    }
}
