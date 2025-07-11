// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteInputSystem(RenderCharacter renderCharacter)

		: QuerySystem<NoteEcs, HitZoneEcs>
	{
		private float endTime;

		protected override void OnUpdate()
		{

			Query.WithoutAllComponents(ComponentTypes.Get<Hit>()).ForEachEntity((ref NoteEcs note, ref HitZoneEcs _, Entity _) =>
			{
				if (note.TimingPoint > endTime)
					endTime = note.TimingPoint + 3;
			});

			foreach (var var in action_shapes)
				getNote(var);
		}

		private void getNote((NoteType noteType, Shape2D shape, string text) tuple)
		{
			if (!Input.IsActionJustPressed(tuple.text)) return;

			var areas = queryShape(tuple.shape);

			Entity? bestEntity = null;
			float minTimingPoint = float.MaxValue;

			foreach (var areaRid in areas)
			{
				Query.HasValue<HitZoneEcs, Rid>(areaRid).ForEachEntity(
					(ref NoteEcs noteComponent, ref HitZoneEcs _, Entity entity) =>
					{
						if (!(noteComponent.TimingPoint < minTimingPoint)|| tuple.noteType != noteComponent.NoteType || Mathf.Abs(noteComponent.TimingPoint - GlobalClock.Instance.PlaybackTimeSec) > 0.3  ) return;

						minTimingPoint = noteComponent.TimingPoint;
						bestEntity = entity;
					});
			}

			bestEntity?.AddComponent(new Hit(GlobalClock.Instance.PlaybackTimeSec, tuple.noteType));
		}

		private Rid[] queryShape(Shape2D shape)
		{
			var query = new PhysicsShapeQueryParameters2D
			{
				Transform = Transform2D.Identity with { Origin = renderCharacter.Position },
				Shape = shape,
				CollideWithAreas = true,
				CollideWithBodies = false,
				CollisionMask = HitZoneEcs.NOTE_AREA_FLAG
			};

			return renderCharacter.GetWorld2D().DirectSpaceState.IntersectShape(query)
				.Select(hitResult => (Rid)hitResult["rid"]).ToArray();
		}


		private static readonly List<(NoteType noteType, Shape2D Shape, string text)> action_shapes =
		[
			(NoteType.Main, new CapsuleShape2D { Radius = 32, Height = 128 }, "main"),
			(NoteType.R1, new ConvexPolygonShape2D { Points = [new(-50, 0),  new (50,0), new(100, -100), new(-100, -100)] },"R1"),
			(NoteType.R2, new ConvexPolygonShape2D { Points = [new(-50, 0),  new (50,0), new(100, 100), new(-100, 100)] },"R2")
		];
	}
}
