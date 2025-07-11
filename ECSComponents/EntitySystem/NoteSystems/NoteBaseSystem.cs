// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;
using static Godot.RenderingServer;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteBaseSystem : QuerySystem<NoteEcs, ElementEcs>
	{

		protected override void OnUpdate() =>
			Query.Each(new UpdateNote());


		private readonly struct UpdateNote : IEach<NoteEcs, ElementEcs>
		{
			private static readonly Vector2 note_size = new(70, 25);
			private static readonly StringName string_name = new("note_pos");


			public void Execute(ref NoteEcs note, ref ElementEcs element)
			{
				// Both the falling note and the stationary receiver need the same timing data
				// for the vertex shader to calculate the note's animated position relative
				// to the hit target.
				update_canvas(element.Canvas, note.TimingPoint); // The receiver (hit target)
				update_canvas(note.NoteCanvas, note.TimingPoint); // The falling note
			}

			/// <summary>
			/// Clears the canvas, updates the shader uniform, and draws the note rectangle.
			/// </summary>
			private static void update_canvas(Rid canvas, float timingPoint)
			{
				CanvasItemClear(canvas);
				CanvasItemSetInstanceShaderParameter(canvas, string_name, timingPoint);
				CanvasItemAddRect(canvas, new Rect2(-note_size / 2, note_size), Colors.White);
			}
		}
	}
}
