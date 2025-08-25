// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Character;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Shaders;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.CharacterSystems
{
	public class GroundEffectsSystems : QuerySystem<CharacterEcs>
	{
		private readonly IVisualsMaster master = DiProvider.Get<IVisualsMaster>();

		private static readonly ParticleProcessMaterial material = new()
		{
			InitialVelocity = new Vector2(0,500),
			Spread = 180,
			Gravity = Vector3.Up * 1000,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			ColorRamp = ParticlesRidExtensions.FadeGradient,
		};

		private readonly ParticlesRid groundParticles = ParticlesRid.Create()
			.SetAmount(100)
			.SetAmountRatio(0)
			.SetLifetime(1)
			.SetProcessMaterial(material.GetRid())
			.SetMesh(MeshFactory.CreateStar(10, 10, 0.5f).GetRid());

		private RenderRid canvas;

		protected override void OnAddStore(EntityStore store)
		{
				canvas = RenderRid.Create(master.GameplayerLayerRid, 1000)
				.AddParticles(groundParticles);

			store.Query<CharacterEcs>().Entities.First().AddSignalHandler<Grounded>(s =>
			{
				ParticlesRid groundHit = ParticlesRid.Create()
					.SetExplosivenessRatio(0.95f)
					.SetLifetime(0.2f)
					.SetOneShot();

				RenderRid.Create(master.GameplayerLayerRid, 2)
					.AddParticles(groundHit)
					.SetTransform(new Transform2D(0, s.Entity.GetComponent<CharacterEcs>().Position));
			});


			store.Query<CharacterEcs>().Entities.First().AddSignalHandler<Airborne>(_ =>
            {
                groundParticles.SetAmountRatio(0);
            });
		}

		protected override void OnUpdate()
		{
			Transform2D xf2d = Transform2D.Identity;
			Query.ForEachEntity((ref CharacterEcs characterEcs, Entity entity) =>
			{
				if (characterEcs.Phase == Phase.Airborne) return;
				groundParticles.SetAmountRatio(Mathf.Abs(characterEcs.Velocity.X / PlayerCharacter.MAX_RUN_SPEED));
				xf2d = Transform2D.Identity with
				{
					Origin =  characterEcs.Position with { Y = characterEcs.Position.Y + PlayerCharacter.CHARACTER_HEIGHT / 2 },
				};
			} );


			Transform3D  xf3d = Transform3D.Identity with { Origin = new Vector3(xf2d.Origin.X, xf2d.Origin.Y, 0) };

			canvas.SetTransform(xf2d);
			ParticlesSetEmissionTransform(groundParticles,xf3d);
		}
	}
}
