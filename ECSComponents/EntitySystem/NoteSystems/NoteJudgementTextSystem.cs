// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public class NoteJudgementTextSystem : QuerySystem
    {
        private ArchetypeQuery<ElementEcs, Hit, Judged> query = null!;
        protected override void OnAddStore(EntityStore store)
        {
            query = store.Query<ElementEcs, Hit, Judged>();
            query.EventFilter.ComponentAdded<Hit>();
        }

        private readonly Font font = ThemeDB.FallbackFont;

        protected override void OnUpdate()
        {
            query.ForEachEntity((ref ElementEcs component1, ref Hit component2,ref Judged component3, Entity entity) =>
            {
                if (!query.HasEvent(entity.Id)) return;
                var v = RenderRid.Create(component1.Canvas, 0.5f);
                font.DrawString(v, Vector2.Zero,component3.Judgement.ToString(), fontSize:50);
            });
        }
    }
}
