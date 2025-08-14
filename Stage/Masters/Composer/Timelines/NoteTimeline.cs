// Copyright (c) mk56_spn
// …

using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Tools;

namespace XanaduProject.Stage.Masters.Composer.Timelines
{
    public partial class NoteTimeline(EntityStore entityStore) : Timeline
    {
        private const float radius = 10f;
        private const float stack_spacing = radius; // vertical distance between stacked notes
        private const int time_quantise = 3; // ms – resolution for “same timing-point”

        protected override void DrawBody()
        {
            // 1) Gather all notes
            var notes = new List<(Entity entity, NoteEcs note)>();
            entityStore.Query<NoteEcs>().ForEachEntity((ref NoteEcs note, Entity entity) =>
            {
                notes.Add((entity, note));
            });

            // 2) Sort: first by note type, then by entity id
            notes.Sort((a, b) =>
            {
                int typeCmp = a.note.NoteType.CompareTo(b.note.NoteType);
                return typeCmp != 0 ? typeCmp : a.entity.Id.CompareTo(b.entity.Id);
            });

            // 3) Draw with a deterministic stack index
            Dictionary<long, int> stackIndex = new();

            foreach (var (entity, note) in notes)
            {
                long key = (long)Math.Round(note.TimingPoint * 1000) / time_quantise;

                int index = stackIndex.GetValueOrDefault(key, 0);
                stackIndex[key] = index + 1;

                Vector2 pos = new(
                    note.TimingPoint * HorizontalScale,
                    index * stack_spacing + 40);

                if (entity.Tags.Has<SelectionFlag>())
                    DrawCircle(pos, radius + 4, XanaduColors.XanaduYellow,
                        false, 2, true);

                DrawCircle(pos, radius, note.NoteType.NoteColor().Darkened(index * 0.1f + 0.1f), antialiased: true);
                DrawCircle(pos, radius - 4, note.NoteType.NoteColor().Darkened(index * 0.1f), antialiased: true);
            }
        }
    }
}
