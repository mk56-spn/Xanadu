// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Stage.Masters.Composer.Timelines;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class NoteTimelineSystem : BaseSystem
    {
        private readonly IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();
        protected override void OnAddStore(EntityStore store)
        {
            Timeline timeline = new NoteTimeline(store);
            visuals.AddTabToMain(timeline);
        }
    }
}
