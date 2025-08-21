// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.Stage.Masters.Rendering;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
	public sealed class NoteCreatorSystem : BaseCreatorSystem<NoteEcs>
    {
        private Notes note;

        protected override void OnUpdate()
        {
            note.C = CommandBuffer;
            Query.EachEntity(note);
            Query.WithoutAnyComponents(ComponentTypes.Get<HoldEcs, DirectionEcs>())
                .ForEachEntity((ref ElementEcs element, ref NoteEcs note, Entity entity) =>
                {
                    note.NoteCanvas.AsRenderRid()
                        .AddCircle(20, color: Colors.Transparent)
                        .AddCircleOutline(30);
                });
        }

        private static ParticleProcessMaterial particlesMaterial = new()
        {
            Spread = 180,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius = 50,
            OrbitVelocity = new Vector2(0.1f,0),
            RadialAccel = Vector2.Down * -10,
            ColorInitialRamp  = new GradientTexture1D
            {
                Gradient = new Gradient
                {
                    Offsets = [0, 1],
                    Colors = [Colors.White.Darkened(0.3f), Colors.Black]
                }
            },
            ColorRamp = new GradientTexture1D
            {
                Gradient = new Gradient
                {
                    Offsets = [0, 1],
                    Colors = [Colors.White, Colors.Transparent]
                }
            }
        };

        private static readonly ParticlesRid particles = ParticlesRid.Create()
            .SetAmount(5)
            .SetMesh(MeshFactory.CreateHeart(10f).GetRid())
            .SetProcessMaterial(particlesMaterial.GetRid());

        private struct Notes : IEachEntity<ElementEcs, NoteEcs>
		{

            public CommandBuffer C;
            private static readonly Vector2 note_size = new(70, 25);

			public void Execute(ref ElementEcs element, ref NoteEcs note, int id)
            {
                RenderRid.Create(element.Canvas)
                    .SetModulate(note.NoteType.NoteColor())
                    .AddParticles(particles)
                    .SetZIndex(-10);

                note.NoteCanvas = RenderRid.Create()
                    .SetMaterial(Materials.Notes.Get().Falling)
                    .SetParent(element.Canvas);

                element.Canvas.AsRenderRid().SetMaterial(Materials.Notes.Get().Receiver);
				    C.AddComponent(id, new HitZoneEcs(PhysicsFactory.CreateNoteArea(element.Transform)));
            }
		}
	}
}
