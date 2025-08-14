// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
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
        }

        private struct Notes : IEachEntity<ElementEcs, NoteEcs>
		{
            public CommandBuffer C;
            private static readonly Vector2 note_size = new(70, 25);

			public void Execute(ref ElementEcs element, ref NoteEcs note, int id)
            {

                note.NoteCanvas = RenderRid.Create()
                    .SetMaterial(Materials.Notes.Get().Falling)
                    .SetParent(element.Canvas)
                    .AddRect(note_size, Colors.White);

                element.Canvas.AsRenderRid().SetMaterial(Materials.Notes.Get().Receiver);
				    C.AddComponent(id, new HitZoneEcs(PhysicsFactory.CreateNoteArea(element.Transform)));
            }
		}
	}
}
