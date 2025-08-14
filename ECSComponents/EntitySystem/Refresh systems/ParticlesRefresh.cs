// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
	public class ParticlesRefresh : BaseRefreshSystem<ParticlesEcs>
	{
		private readonly GradientTexture1D gradientTexture1D = new() { Gradient = new Gradient() };

		protected override void OnUpdate()
		{
			foreach (var (elements, particles, entities) in Query.Chunks)
				for (int n = 0; n < entities.Length; n++)
					RenderingServer.CanvasItemAddParticles(elements[n].Canvas, particles[n].Particles,
						gradientTexture1D.GetRid());
		}
	}
}
