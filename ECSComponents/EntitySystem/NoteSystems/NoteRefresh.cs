// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem.Refresh_systems;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Rendering;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteRefresh : BaseRefreshSystem<NoteEcs>
	{
		private readonly IClock clock = DiProvider.Get<IClock>();

		protected sealed override void OnUpdate()
		{
			Query.Each(new UpdateNote());
		}

		protected override void OnAddStore(EntityStore store)
		{
			base.OnAddStore(store);

			clock.Stopped += () =>
			{
				var buffer = store.GetCommandBuffer();
				GD.Print("stopped");
				store.Query<NoteEcs, ElementEcs>().ForEachEntity((ref NoteEcs _, ref ElementEcs _, Entity entity) =>
				{
					buffer.RemoveTag<Judged>(entity.Id);
					buffer.RemoveComponent<Hit>(entity.Id);
				});
				buffer.Playback();
			};
		}
		private readonly struct UpdateNote : IEach<ElementEcs,NoteEcs>
		{
			private static readonly Vector2 note_size = new(70, 25);

			/// <summary>
			/// Clears the canvas, updates the shader uniform, and draws the note rectangle.
			/// </summary>
			private static void update_canvas(Rid canvas, NoteEcs note)
			{
                InstanceShaders.SetNoteTime(canvas, note.TimingPoint);
                CanvasItemSetSelfModulate(canvas, note.NoteType.NoteColor());
			}

            public void Execute(ref ElementEcs element, ref NoteEcs note)
            {
                // Both the falling note and the stationary receiver need the same timing data
                // for the vertex shader to calculate the note's animated position relative
                // to the hit target.
                update_canvas(element.Canvas, note); // The receiver (hit target)
                update_canvas(note.NoteCanvas, note); // The falling note

            }
        }
	}
}
