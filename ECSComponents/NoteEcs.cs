// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents
{
	public struct NoteEcs(float timingPoint, NoteType note) : IComponent, IComparable<float>, IUpdatable
	{
		public static readonly int RADIUS = 32;

		[Composer("Note")]
		public readonly NoteType Note = note;

		[Composer("position")]
		public bool CenterPlayer;

		[Ignore]
		public bool IsHit = false;
		public float TimingPoint = timingPoint;

		public int CompareTo(float other) =>
			TimingPoint.CompareTo(other);


		public void Update(ElementEcs element)
		{
			CanvasItemAddCircle(element.Canvas, new Vector2(0, 0), RADIUS * 1.1f, Colors.White.Darkened(0.1f));
			CanvasItemAddCircle(element.Canvas, new Vector2(0, 0), RADIUS, Colors.White);
			particlesCreate(element.Canvas);
		}

		private void particlesCreate(Rid canvas)
		{
			// The usual rendering server boilerplate attaching of your new canvas to whatever canvas you please
			Rid r = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(r, canvas);


			Rid particles = RenderingServer.ParticlesCreate();

			//Ensure the mode matches
			RenderingServer.ParticlesSetMode(particles, RenderingServer.ParticlesMode.Mode2D);

			// A mesh of your choosing.
			// an upcoming PR will add custom 2d meshes but till then the easiest way to obtain a mesh for testing is to have
			// a sprite 2d and convert it to one in the editor
			Rid myMesh = GD.Load<ArrayMesh>("uid://m1gavyt4hc1g").GetRid();

			RenderingServer.ParticlesSetDrawPasses(particles, 1);
			RenderingServer.ParticlesSetDrawPassMesh(particles, 0, myMesh);

			// Particles seem to default to not emitting
			RenderingServer.ParticlesSetEmitting(particles,true);
			RenderingServer.ParticlesSetAmount(particles, 100);

			RenderingServer.ParticlesSetLifetime(particles, 1);

			// No idea what it defaults to , not necessary for particles to display but good to have just in case its very high by default
			RenderingServer.ParticlesSetFixedFps(particles, 120);
			RenderingServer.ParticlesSetDrawOrder(particles, RenderingServer.ParticlesDrawOrder.Lifetime);

			// The usual particles setup you´d do in the editor, in fact unless you need to do it procedurally youd be better off testing it on a regular particles node and then saving it for direct usage
			ParticleProcessMaterial material = new ParticleProcessMaterial
			{
				InitialVelocity = new Vector2(20, 200),
			};

			// Your texture of choosing
			GradientTexture2D gradientTexture2D  = GD.Load<GradientTexture2D>("uid://ywrfawtk72po");

			RenderingServer.ParticlesSetProcessMaterial(particles, material.GetRid());
			RenderingServer.CanvasItemAddParticles(r, particles, gradientTexture2D.GetRid() );
		}
	}
	public enum NoteType
	{
		Left,
		Right,
		Up,
		Down
	}
}
