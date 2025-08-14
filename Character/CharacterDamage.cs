// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Character
{
    public partial class CharacterDamage : Node2D
    {
        private static World2D world => DiProvider.Get<IPhysicsMaster>().World2D;

        private readonly IStageConductor stageConductor = null!;

        public override void _PhysicsProcess(double delta)
        {
            if (queryDamage())
                stageConductor.DamageTaken();
        }


        private PhysicsShapeQueryParameters2D query =  new()
        {
            Shape = new CircleShape2D { Radius = 32 },
            CollideWithAreas = true,
            CollisionMask = PhysicsFactory.DAMAGE_AREA
        };

        private bool queryDamage()
        {
            query.Transform = GlobalTransform;
            return world.DirectSpaceState
                .IntersectShape(query).Count != 0;
        }
    }
}
