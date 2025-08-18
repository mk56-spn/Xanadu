// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteResultSystem : QuerySystem<NoteEcs, Hit, ElementEcs>
	{
		private readonly IClock clock = DiProvider.Get<IClock>();

		protected override void OnUpdate()
		{
			Query.WithoutAllComponents(ComponentTypes.Get<Judged>()).ForEachEntity((ref NoteEcs note, ref Hit _, ref ElementEcs element, Entity _) =>
				setupNoteVisuals(note, element));

			Query.WithoutAllComponents(ComponentTypes.Get<Judged>()).ForEachEntity((ref NoteEcs _, ref Hit _, ref ElementEcs _, Entity entity) =>
					noteCharacterUpdate(entity));

			setupJudgedComponent();
		}

		private void noteCharacterUpdate(Entity entity)
		{
			GD.Print("noteCharacterUpdate");
			if (entity.TryGetComponent(out DirectionEcs direction))
				DiProvider.Get<IPlayerCharacter>().TriggerDirectedAcceleration(direction.Direction);
			if (entity.TryGetComponent(out HoldEcs hold))
			{
				GD.Print("entity hold");
				DiProvider.Get<IPlayerCharacter>().TriggerHold(hold.Duration);
			}
		}

		private readonly ShaderMaterial material = new() { Shader = GD.Load<Shader>("uid://cyrqfv0y5md53") };

		private static readonly Vector2 size = new(400, 400);

		private void setupNoteVisuals(NoteEcs note, ElementEcs element)
		{
			var rid = RenderRid.Create(element.Canvas);
				rid.SetZIndex(30)
				.AddRect(size)
				.SetParent(element.Canvas)
				.SetMaterial(material.GetRid())
				.SetModulate(note.NoteType.NoteColor())
				.SetLifetime(0.7f);

			RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "hit_pos", clock.PlaybackTimeSec);

			clock.Stopped += () => RenderingServer.FreeRid(rid);
		}

		private void setupJudgedComponent()
		{
			var command = CommandBuffer;
			Query.ForEachEntity((ref NoteEcs note, ref Hit hit,
				ref ElementEcs _, Entity entity) =>
			{
				float f = (float)(hit.Time - note.TimingPoint) * 1000;
				command.AddComponent(entity.Id, new Judged
				{
					Judgement = JudgementInfo.GetJudgement(Mathf.Abs(f)),
					Deviation = f
				});
			});
			command.Playback();
		}
	}
}
