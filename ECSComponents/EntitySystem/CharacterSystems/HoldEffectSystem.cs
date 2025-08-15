// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Character;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.CharacterSystems
{
    public class HoldEffectSystem : QuerySystem<CharacterEcs>
    {
        private readonly IPlayerCharacter player = DiProvider.Get<IPlayerCharacter>();

        private static readonly ParticleProcessMaterial material = new()
        {
            Spread = 180,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            InitialVelocityMin = 100,
            InitialVelocityMax = 200,
            Gravity = new Vector3(0, 100,0),
            Color = Colors.Red,
            ColorInitialRamp = new GradientTexture1D{ Gradient = new Gradient()}
        };
        private readonly ParticlesRid particles;

        public HoldEffectSystem()
        {
            RenderRid.Create(player.PlayerCanvasRid)
                .AddParticles(particles = ParticlesRid.Create()
                    .SetAmount(100)
                    .SetLifetime(1F)
                    .SetProcessMaterial(material.GetRid())
                    .SetMesh(MeshFactory.CreateHeart(10).GetRid())
                    );
        }
        protected override void OnUpdate()
        {
            bool emit = player.StateMachine.State is MovementState.Holding or MovementState.MovingAndHolding;
            particles.SetEmitting(emit);
        }
    }
}
