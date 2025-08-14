// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
		private List<(Judgement, double)> judgements = new();
		private IClock clock = DiProvider.Get<IClock>();

		public NoteResultSystem()
		{
			clock.Started += () =>
			{
				judgements = [];
				foreach (var variable in DiProvider.Get<IUiMaster>().ScoreLayer.GetChildren().OfType<Results>())
					variable.QueueFree();

			};

			Filter.WithoutAllTags(Tags.Get<Judged>());
		}

		protected override void OnUpdate()
		{
			Query.ForEachEntity((ref NoteEcs note, ref Hit _, ref ElementEcs element, Entity entity) =>
				setupNoteVisuals(note, element));

			Query.ForEachEntity((ref NoteEcs note, ref Hit _, ref ElementEcs element, Entity entity) =>
					noteCharacterUpdate(note, element, entity));

			setupResultText();
		}

		private void noteCharacterUpdate(NoteEcs note, ElementEcs element, Entity entity)
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

		private void setupResultText()
		{
			var command = CommandBuffer;
			Filter.AllComponents(default);
			Query.ForEachEntity((ref NoteEcs note, ref Hit hit,
				ref ElementEcs component3, Entity entity) =>
			{
				Label result;
				DiProvider.Get<IUiMaster>().ScoreLayer.AddChild(result = new Label
				{
					Position = component3.Transform.Origin,
					Text = JudgementInfo.GetJudgmentText(
						JudgementInfo.GetJudgement(Math.Abs(hit.Time - note.TimingPoint) * 1000))
				});

				DiProvider.Get<IUiMaster>().ScoreLayer.GetTree().CreateTimer(0.4f).Timeout += () => result.QueueFree();
				command.AddTag<Judged>(entity.Id);
			});

			command.Playback();
		}
	}
}
