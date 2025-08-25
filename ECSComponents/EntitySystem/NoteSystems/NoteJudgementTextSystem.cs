// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Animation;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public class NoteJudgementTextSystem : QuerySystem
    {
        private readonly IClock clock = DiProvider.Get<IClock>();
        private ArchetypeQuery<ElementEcs, Hit, Judged> query = null!;
        protected override void OnAddStore(EntityStore store)
        {
            query = store.Query<ElementEcs, Hit, Judged>();
            query.EventFilter.ComponentAdded<Hit>();
            clock.Stopped += () =>
            {
                foreach (var rid in renderRids)
                    rid.Clear();
            };
        }

        private readonly Font font = ThemeDB.FallbackFont;

        private readonly List<RenderRid> renderRids = new(300);
        private readonly AnimationMaterial material = new();
        protected override void OnUpdate()
        {
            query.ForEachEntity((ref ElementEcs component1, ref Hit component2,ref Judged component3, Entity entity) =>
            {
                if (!query.HasEvent(entity.Id)) return;
                var v = RenderRid.Create(component1.Canvas, 5f);

                v.SetMaterial(material.GetRid());
                material.SetAnimationStartCurrent(v);
                material.SetAnimationDuration(v,0.5f);
                material.SetEasingIndex(v,EasingType.OutExpo);

                string text = component3.Judgement.ToString();

                font.DrawString(v, -font.GetStringSize(text, fontSize: 50) / 2,text, fontSize:50);
                renderRids.Add(v);
            });
        }
    }
}
