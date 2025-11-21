// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem.NoteSystems;
using XanaduProject.ECSComponents.EntitySystem.Refresh_systems;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class RefreshGroup : SystemGroup
    {
        public RefreshGroup(string name) : base(name)
        {
            Add(new MainRefreshSystem());
            Add(new PolygonRefresh());
            Add(new ParticlesRefresh());
            Add(new BlockRefresh());
            Add(new NoteRefresh());
            Add(new TriangleArrayRefresh());
            Add(new RefreshFinalizer());
            Add(new BackgroundSystem());
        }
    }
}
