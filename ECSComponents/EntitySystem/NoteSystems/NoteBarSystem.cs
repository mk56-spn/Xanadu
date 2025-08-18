// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using static XanaduProject.DataStructure.JudgementInfo;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public class NoteBarSystem : QuerySystem
    {
        private readonly IUiMaster visualsMaster = DiProvider.Get<IUiMaster>();
        private readonly RenderRid bar;

        private const float bar_scale = 3;
        public NoteBarSystem()
        {
            bar = RenderRid.Create(visualsMaster.TopCenterUi.GetCanvasItem())
                .SetTransform(new Transform2D(0, new Vector2(0, 50)));

            foreach (var j in Enum.GetValues<Judgement>().Reverse())
            {
                float f = (float)JudgementDeviation(j);
                bar.AddLine(new Vector2(-f * bar_scale,0), new Vector2(f * bar_scale, 0), GetJudgmentColor(j));
            }
            Filter.AnyComponents(ComponentTypes.Get<Hit>());
        }

        private ArchetypeQuery<Judged> query = null!;

        protected override void OnAddStore(EntityStore store)
        {
            query = store.Query<Judged>();
            query.EventFilter.ComponentAdded<Hit>();
        }

        protected override void OnUpdate()
        {
            query.ForEachEntity((ref Judged judged, Entity entity) =>
            {
                if (!query.HasEvent(entity.Id)) return;
                var v = RenderRid.Create(bar, 0.5f);
                v.AddRect(new Rect2(new Vector2(judged.Deviation * bar_scale, 0), new Vector2(1, 20)),
                    GetJudgmentColor(judged.Judgement));
            });
        }
    }
}
