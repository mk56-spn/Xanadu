// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Mesh;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Stage.Masters.Composer
{
    public static class EntityTemplate
    {
        private static readonly EntityStore pseudo_store = new();

        public static readonly ArchetypeQuery<NameEcs> GET_ENTITIES = pseudo_store.Query<NameEcs>();

        static EntityTemplate()
        {

            createMiscTemplates();
        }
        private static void createMiscTemplates()
        {
            pseudo_store.CreateEntity(new HurtZoneEcs(), new NameEcs("HurtZone"));
            pseudo_store.CreateEntity(new BlockEcs(),new RectEcs(new(32,32)), new NameEcs("Block"));
            pseudo_store.CreateEntity(new NoteEcs(), new NameEcs("♪"));
            pseudo_store.CreateEntity(new NoteEcs(), new HoldEcs(),new NameEcs("♪─"));
            pseudo_store.CreateEntity(new MeshEcs(), new NameEcs("Element"));
            pseudo_store.CreateEntity(new RectEcs(new(32,32)));
        }

        public static Entity GetDefault() => pseudo_store.GetEntityById(1);
    }
}
