// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public abstract class BaseRefreshSystem : QuerySystem<ElementEcs>
    {
        protected BaseRefreshSystem()=>
            Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());
    }
    public abstract class BaseRefreshSystem<T1> : QuerySystem<ElementEcs, T1> where T1 : struct, IComponent
    {
        protected BaseRefreshSystem()=>
            Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());
    }
}
