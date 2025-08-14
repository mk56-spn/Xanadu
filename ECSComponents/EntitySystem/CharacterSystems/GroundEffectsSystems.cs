// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Character;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.CharacterSystems
{
	public class GroundEffectsSystems : QuerySystem<CharacterEcs>
	{
		private readonly IVisualsMaster master = DiProvider.Get<IVisualsMaster>();

		private static readonly ParticleProcessMaterial material = new()
		{
			Spread = 180,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			InitialVelocityMin = 100,
			InitialVelocityMax = 200,
			ColorRamp = new GradientTexture1D { Gradient = new Gradient() },
		};

		private readonly ParticlesRid groundParticles = ParticlesRid.Create()
			.SetAmount(1000)
			.SetEmitting(false)
			.SetLifetime(3)
			.SetProcessMaterial(material.GetRid())
			.SetMesh(MeshFactory.CreateStar(10, 10, 0.5f).GetRid());


		private RenderRid canvas;

		protected override void OnAddStore(EntityStore store)
		{

		   canvas = RenderRid.Create(master.GameplayerLayerRid, 1000)
			   .AddParticles(groundParticles);

			store.Query<CharacterEcs>().Entities.First().AddSignalHandler<Grounded>(s =>
			{
				groundParticles.SetEmitting();

				ParticlesRid groundHit = ParticlesRid.Create()
					.SetExplosivenessRatio(0.95f)
					.SetLifetime(0.2f)
					.SetOneShot();

				RenderRid.Create(master.GameplayerLayerRid, 2)
					.AddParticles(groundHit)
					.SetTransform(new Transform2D(0, s.Entity.GetComponent<CharacterEcs>().Position));
			});


			store.Query<CharacterEcs>().Entities.First().AddSignalHandler<Airborne>(_ =>
				groundParticles.SetEmitting(false));
		}



		protected override void OnUpdate()
		{

			Query.ForEachEntity((ref CharacterEcs component1, Entity entity) =>
			{
				groundParticles.SetAmountRatio(0.1f);
				canvas
					.SetTransform(new Transform2D(0, component1.Position));
			} );
		}
	}
}
