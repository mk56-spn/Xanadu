// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class ParticlesRefresh : QuerySystem<ElementEcs, ParticlesEcs>
    {
        private GradientTexture1D gradientTexture1D = new()
        {
            Gradient = new Gradient()
        };

        public ParticlesRefresh() => Filter.AllTags(Tags.Get<Dormant, SelectionFlag>());

        protected override void OnUpdate()
        {

            foreach (var (elements, particles, entities) in Query.Chunks)
                for (int n = 0; n < entities.Length; n++)
                    RenderingServer.CanvasItemAddParticles(elements[n].Canvas, particles[n].Particles, gradientTexture1D.GetRid());
        }
    }
}
