// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;

namespace XanaduProject.Stage.Masters.Composer
{
    public static class EntityTemplate
    {
        private static readonly EntityStore pseudo_store = new();

        public static readonly ArchetypeQuery<NameEcs> GET_ENTITIES = pseudo_store.Query<NameEcs>();

        static EntityTemplate()
        {
            createNoteTemplates();
            createBlockTemplates();
            createMiscTemplates();

            GD.Print("We have " + GET_ENTITIES.Count + " templates");
        }

        private static void createNoteTemplates()
        {
            // Note templates no longer have hardcoded directions.
            pseudo_store.CreateEntity(new NoteEcs(NoteType.L), new NameEcs("L"));
            pseudo_store.CreateEntity(new NoteEcs(NoteType.C), new NameEcs("C"));
            pseudo_store.CreateEntity(new NoteEcs(NoteType.R), new NameEcs("R"));
            pseudo_store.CreateEntity(new NoteEcs(NoteType.Main), new NameEcs("Main"));
        }

        private static readonly Vector2[] presets =
        [
            new(32, 32),
            new(64, 64),
            new(128, 128),
            new(128, 32),
            new(256, 256),
            new(10000, 10000)
        ];
        private static void createBlockTemplates()
        {
            foreach (var preset in presets)
                pseudo_store.CreateEntity(new RectEcs(preset), new BlockEcs(), new NameEcs(preset.ToString()));
        }

        private static void createMiscTemplates()
        {
            pseudo_store.CreateEntity(new TriangleArrayEcs(), new NameEcs("Array"));
            pseudo_store.CreateEntity(new PolygonEcs(), new NameEcs("Polygon"));
            pseudo_store.CreateEntity(new HurtZoneEcs(), new NameEcs("HurtZone"));
        }

        public static Entity GetDefault() => pseudo_store.GetEntityById(0);
    }
}
