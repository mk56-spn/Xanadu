// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public abstract class ComposerSystem : QuerySystem<ElementEcs>
    {
        public readonly IComposerVisuals Visuals = DiProvider.Get<IComposerVisuals>();
        protected readonly IComposer Composer = DiProvider.Get<IComposer>();
        protected ComposerSystem()=> Filter.AnyTags(Tags.Get<SelectionFlag>());
    }
}
