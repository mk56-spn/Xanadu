// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public abstract class BaseCreatorSystem : QuerySystem<ElementEcs> {
        protected BaseCreatorSystem() => Filter.AnyTags(Tags.Get<UnInitialized>());
    }

    public abstract class BaseCreatorSystem<T1> : QuerySystem<ElementEcs, T1>
        where T1 : struct, IComponent {
        protected BaseCreatorSystem() => Filter.AnyTags(Tags.Get<UnInitialized>());
    }
    public abstract class BaseCreatorSystem<T1, T2> : QuerySystem<ElementEcs, T1, T2>
        where T1 : struct, IComponent
        where T2 : struct, IComponent {
        protected BaseCreatorSystem() => Filter.AnyTags(Tags.Get<UnInitialized>());
    }

}
