// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.Colors;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents
{
    [ComponentKey(null)]
    public struct ParticlesEcs : IComponent, IUpdatable
    {
        public Rid Particles;

        ParticleProcessMaterial material = GD.Load<ParticleProcessMaterial>("uid://c4kcv6dwvgawm");
        ArrayMesh mesh = GD.Load<ArrayMesh>("uid://devv00es6ij2q");

        private readonly GradientTexture2D gradientTexture2D = new()
        {
            Width = 10,
            Height = 10,
            Gradient = new Gradient
            {
                Colors = [Transparent, White, Transparent],
                Offsets = [0,0.5f,1],
            },
        };

        public ParticlesEcs()
        {
            Particles = ParticlesCreate();

            //Ensure the mode matches
            ParticlesSetMode(Particles, ParticlesMode.Mode2D);

            // A mesh of your choosing.
            // an upcoming PR will add custom 2d meshes but till then the easiest way to obtain a mesh for testing is to have
            // a sprite 2d and convert it to one in the editor

            ParticlesSetDrawPasses(Particles, 1);

            ParticlesSetDrawPassMesh(Particles, 0, mesh.GetRid());

            // Particles seem to default to not emitting
            ParticlesSetEmitting(Particles, true);
            ParticlesSetAmount(Particles, 2);

            ParticlesSetLifetime(Particles, 1);

            // No idea what it defaults to , not necessary for particles to display but good to have just in case its very high by default
            ParticlesSetFixedFps(Particles, 120);
            ParticlesSetDrawOrder(Particles, ParticlesDrawOrder.ReverseLifetime);

            // Your texture of choosing

            ParticlesSetProcessMaterial(Particles, material.GetRid());


        }
        public void Update(ElementEcs elementEcs)
        {
            CanvasItemAddParticles(elementEcs.Canvas, Particles, gradientTexture2D.GetRid());
        }
    }
}
