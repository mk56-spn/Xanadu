// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public class HoldNoteCreator : BaseCreatorSystem<NoteEcs, HoldEcs>
    {
        private readonly HoldCreator creator = new();
        protected override void OnUpdate()
        {
            Query.Each(creator);
        }

        private static readonly ParticleProcessMaterial material = new()
        {
            Spread = 0,
            InitialVelocity = new Vector2(300,300),
            Gravity = Vector3.Zero,
            Angle = new Vector2(-130, 20),

            ColorRamp = ParticlesRidExtensions.FadeGradient
        };

        private struct HoldCreator : IEach<ElementEcs,NoteEcs,HoldEcs>
        {
            public void Execute(ref ElementEcs c1, ref NoteEcs c2, ref HoldEcs c3)
            {
                RenderRid.Create(c2.NoteCanvas)
                    .AddParticles(ParticlesRid.Create()
                            .SetAmount(5)
                            .SetProcessMaterial(material.GetRid()));


                c2.NoteCanvas.AsRenderRid();
            }
        }
    }
}
