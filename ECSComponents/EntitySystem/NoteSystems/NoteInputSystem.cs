// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
	public class NoteInputSystem : QuerySystem<NoteEcs, HitZoneEcs>
	{
		private readonly IClock clock = DiProvider.Get<IClock>();

		private float endTime;

		protected override void OnUpdate()
		{
			Query.WithoutAllComponents(ComponentTypes.Get<Hit>())
				.ForEachEntity((ref NoteEcs note, ref HitZoneEcs _, Entity _) =>
				{
					if (note.TimingPoint > endTime)
						endTime = note.TimingPoint + 3;
				});

			foreach (var noteAction in NoteTypeUtils.ACTION_SHAPES)
				getNote(noteAction);
		}

		private void getNote((NoteType noteType, Shape2D shape, StringName text) tuple)
		{
			if (!Input.IsActionJustPressed(tuple.text)) return;

			var areas = PhysicsFactory.QueryNotesAreas(tuple.shape);

			Entity? bestEntity = null;
			float minTimingPoint = float.MaxValue;

			foreach (var areaRid in areas)
				Query.HasValue<HitZoneEcs, Rid>(areaRid)
					.ForEachEntity((ref NoteEcs noteComponent, ref HitZoneEcs _, Entity entity) =>
					{
						if (!(noteComponent.TimingPoint < minTimingPoint) || tuple.noteType != noteComponent.NoteType ||
							Mathf.Abs(noteComponent.TimingPoint - clock.PlaybackTimeSec) > 0.3) return;

						minTimingPoint = noteComponent.TimingPoint;
						bestEntity = entity;
					});

			bestEntity?.AddComponent(new Hit(clock.PlaybackTimeSec, tuple.noteType));
		}
	}
}
