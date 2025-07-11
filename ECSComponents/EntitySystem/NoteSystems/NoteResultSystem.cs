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
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;
using XanaduProject.Screens;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteResultSystem : QuerySystem<NoteEcs, Hit, ElementEcs>
	{
		private List<(Judgement, double)> judgements = new();
		private readonly RenderCharacter renderCharacter;
		private readonly CanvasLayer canvasLayer;

		public NoteResultSystem( RenderCharacter renderCharacter, CanvasLayer canvasLayer)
		{
			this.renderCharacter = renderCharacter;
			this.canvasLayer = canvasLayer;

			GlobalClock.Instance.Started+= () =>
			{
				judgements = [];
				foreach (var variable in canvasLayer.GetChildren().OfType<Results>()) variable.QueueFree();
			};

			Filter.WithoutAllTags(Tags.Get<Judged>());
		}

		protected override void OnUpdate()
		{

			Query.AllComponents(ComponentTypes.Get<DirectionEcs>()).ForEachEntity(
				(ref NoteEcs note, ref Hit _, ref ElementEcs element, Entity entity) =>
				{
					var direction = entity.GetComponent<DirectionEcs>();
					if (note.CenterPlayer)
						renderCharacter.Position = element.Transform.Origin;

					float finalStrength = DirectionEcs.BASE_STRENGTH * getStrength(direction.Strength);

					var targetVelocity = direction.Direction switch
					{
						Direction.Left    => renderCharacter.Velocity with { X = -finalStrength },
						Direction.Up      => renderCharacter.Velocity with { Y = -finalStrength },
						Direction.Right   => renderCharacter.Velocity with { X = finalStrength },
						Direction.Down    => renderCharacter.Velocity with { Y = finalStrength },
						Direction.UpLeft  => renderCharacter.Velocity with { X = -finalStrength, Y = -finalStrength },
						Direction.UpRight => renderCharacter.Velocity with { X = finalStrength,  Y = -finalStrength },
						_ => renderCharacter.Velocity with { X = 0, Y = 0}
					};


					renderCharacter.SetVelocity(targetVelocity);
				} );

			var command = CommandBuffer;
			Filter.AllComponents(default);
			Query.ForEachEntity(((ref NoteEcs note, ref Hit hit,
				ref ElementEcs component3, Entity entity) =>
			{
				Label result;
				canvasLayer.AddChild(result = new Label()
				{
					Position = component3.Transform.Origin,
					Text = JudgementInfo.GetJudgmentText(JudgementInfo.GetJudgement(Math.Abs(hit.Time - note.TimingPoint) * 1000))
				});

				canvasLayer.GetTree().CreateTimer(0.4).Timeout += () => result.QueueFree();
				command.AddTag<Judged>(entity.Id);

			}));

			command.Playback();
		}

		private float getStrength(Strength strength)
		{
			return strength switch
			{
				Strength.Weak => 0.75f,
				Strength.Medium => 1,
				Strength.Strong => 1.35f,
				_ => throw new ArgumentOutOfRangeException()
			};
		}
	}
}
