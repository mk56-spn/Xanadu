// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public class HurtZoneCreator : BaseCreatorSystem<HurtZoneEcs>
    {
        protected override void OnUpdate()
        {
            Query.Each(new CreateHurtZone());
        }
        private struct CreateHurtZone : IEach<ElementEcs,HurtZoneEcs>
        {
            public void Execute(ref ElementEcs element, ref HurtZoneEcs hurtZone)
            {
                hurtZone.Area = PhysicsFactory.CreateHurtAreaRound();
            }
        }
    }
}
