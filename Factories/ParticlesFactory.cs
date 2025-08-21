// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using Godot;
using JetBrains.Annotations;
using static Godot.RenderingServer;

namespace XanaduProject.Factories
{
    /// <summary>
    /// Helper wrapper so a particle <see cref="Rid"/> can be fluently configured.
    /// The struct is zero-cost – it is implicitly convertible to and from <see cref="Rid"/>.
    /// </summary>
    public readonly struct ParticlesRid(Rid rid)
    {
        private readonly Rid internalRid = rid;

        #region CTORS

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParticlesRid Create(bool oneShot = false, float lifetime = 10000f, int particleCount = 8)
        {
            var particles = new ParticlesRid(ParticlesCreate());

            //Ensure the mode matches
            ParticlesSetMode(particles, ParticlesMode.Mode2D);

            ParticlesSetDrawPasses(particles, 1);
            ParticlesSetDrawPassMesh(particles, 0, ARROW_MESH.GetRid());

            // Set one shot based on parameter
            ParticlesSetOneShot(particles, oneShot);

            // No idea what it defaults to, not necessary for particles to display but good to have just in case its very high by default
            ParticlesSetFixedFps(particles, 120);

            particles
                .SetAmount(particleCount)
                .SetEmitting()
                .SetLifetime(lifetime)
                .SetDrawOrder(ParticlesDrawOrder.ReverseLifetime)
                .SetProcessMaterial(GROUNDHIT.GetRid());

            return particles;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParticlesRid Create()
        {
            var particles = new ParticlesRid(ParticlesCreate());

            //Ensure the mode matches
            ParticlesSetMode(particles, ParticlesMode.Mode2D);

            ParticlesSetDrawPasses(particles, 1);
            ParticlesSetDrawPassMesh(particles, 0, MeshFactory.CreateTriangle(20).GetRid());

            // Having the entire game crash because the particle amount wasn't set is apparently a thing
            ParticlesSetOneShot(particles,false);

            // No idea what it defaults to, not necessary for particles to display but good to have just in case its very high by default
            ParticlesSetFixedFps(particles, 120);

            particles
                .SetAmount(8)
                .SetEmitting()
                .SetLifetime(1)
                .SetDrawOrder(ParticlesDrawOrder.ReverseLifetime)
                .SetProcessMaterial(GROUNDHIT.GetRid());

            return particles;
        }


        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Rid(ParticlesRid r) => r.internalRid;


        [UsedImplicitly] public static readonly ParticleProcessMaterial DIRECTIONAL_MATERIAL =
            GD.Load<ParticleProcessMaterial>("uid://c4kcv6dwvgawm");

        [UsedImplicitly] public static readonly ParticleProcessMaterial MATERIAL =
            GD.Load<ParticleProcessMaterial>("uid://b2k2vileav4cl");

        [UsedImplicitly] public static readonly ArrayMesh CIRCLE_MESH = GD.Load<ArrayMesh>("uid://bsd375l20sim8");
        [UsedImplicitly] public static readonly ArrayMesh ARROW_MESH = GD.Load<ArrayMesh>("uid://devv00es6ij2q");

        [UsedImplicitly]
        public static readonly ParticleProcessMaterial GROUNDHIT =
            GD.Load<ParticleProcessMaterial>("uid://c1muvg3gygr2o");

    }

    /// <summary>
    /// Chainable extensions that wrap the RenderingServer.Particles* methods.
    /// </summary>
    public static class ParticlesRidExtensions
    {
        public static GradientTexture1D FadeGradient = new()
        {
            Gradient = new Gradient
            {
                Offsets = [0, 0.5f, 1],
                Colors = [Colors.White, Colors.Transparent]
            }
        };
        public static ParticlesRid SetEmitting(this in ParticlesRid p, bool emitting = true)
        {
            ParticlesSetEmitting(p, emitting);
            return p;
        }

        public static ParticlesRid SetAmount(this in ParticlesRid p, int amount)
        {
            ParticlesSetAmount(p, amount);
            return p;
        }

        public static ParticlesRid SetAmountRatio(this in ParticlesRid p, float amount)
        {
            ParticlesSetAmountRatio(p, amount);
            return p;
        }

        public static ParticlesRid SetExplosivenessRatio(this in ParticlesRid p, float ratio)
        {
            ParticlesSetExplosivenessRatio(p, ratio);
            return p;
        }

        public static ParticlesRid SetLifetime(this in ParticlesRid p, float lifetime)
        {
            ParticlesSetLifetime(p, lifetime);
            return p;
        }

        public static ParticlesRid SetDrawOrder(this in ParticlesRid p, ParticlesDrawOrder order)
        {
            ParticlesSetDrawOrder(p, order);
            return p;
        }

        public static ParticlesRid SetProcessMaterial(this in ParticlesRid p, Rid material)
        {
            ParticlesSetProcessMaterial(p, material);
            return p;
        }

        public static ParticlesRid SetOneShot(this in ParticlesRid p, bool oneShot = true)
        {
            ParticlesSetOneShot(p, oneShot);
            return p;
        }

        public static ParticlesRid SetMesh(this in ParticlesRid p, Rid mesh)
        {
            ParticlesSetDrawPassMesh(p,0, mesh);
            return p;
        }

        public static ParticlesRid SetEmissionTransform(this in ParticlesRid p, Transform2D xf2d)
        {
            // This has to be the most retarded design ever...
            Transform3D  xf3d = Transform3D.Identity with { Origin = new Vector3(xf2d.Origin.X, xf2d.Origin.Y, 0) };
            ParticlesSetEmissionTransform(p,xf3d);
            return p;
        }
    }
}
