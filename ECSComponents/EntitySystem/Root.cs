// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.EcGui;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EcGuiSetup;
using XanaduProject.ECSComponents.EntitySystem.NoteSystems;
using XanaduProject.ECSComponents.EntitySystem.Refresh_systems;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class Root : SystemRoot
    {
        private readonly EntityStore entityStore;
        private readonly CanvasLayer scoreLayer = new() { Layer = 2 };
        private readonly CanvasLayer gameplayLayer = new() { Layer = 2, FollowViewportEnabled = true};

        public Root(EntityStore entityStore, RenderCharacter renderCharacter,
            RenderMaster renderMaster)
        {
            var buffer = entityStore.GetCommandBuffer();

            entityStore.Query<ElementEcs>().EachEntity(new EachInitialiser(buffer));

            buffer.Playback();

            this.entityStore = entityStore;
            renderMaster.AddChild(scoreLayer);
            renderMaster.AddChild(gameplayLayer);

            AddStore(entityStore);

            EntityList list  = entityStore.Query<FloatArrayEcs>().ToEntityList();

            // Entity updaters
            Add(new InitializerSystem(entityStore, renderMaster));

            refreshSystems();

            // Note systems
            Add(new NoteHitSystem());
            Add(new NoteInputSystem( renderCharacter));
            Add(new NoteResultSystem( renderCharacter, gameplayLayer));
            Add(new BarSystem(scoreLayer));

            Add(new ColourInterpolatorSystem(entityStore));
            Add(new DebugSystem(scoreLayer, entityStore, this));
            Add(new NoteBaseSystem());



            EcGui.AddExplorerStore("Store", entityStore);
            TypeDrawers.Register();
            EcGui.AddExplorerSystems(this);

            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Id));
            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Vector2));
            EcGui.Explorer.AddComponentMemberColumn<ActiveColourEcs>(nameof(ActiveColourEcs.Color));

            var elements = entityStore.Query<ElementEcs>();
            EcGui.AddExplorerQuery("elements", elements);
            var array = entityStore.Query<FloatArrayEcs>();
            EcGui.AddExplorerQuery("arrays", array);




            entityStore.OnEntityDelete += delete =>
            {
                var entity = delete.Entity;
                RenderingServer.FreeRid(entity.GetComponent<ElementEcs>().Canvas);
                PhysicsServer2D.FreeRid(entity.GetComponent<SelectionEcs>().Area);

                if (entity.TryGetComponent(out HitZoneEcs hitZone))
                    PhysicsServer2D.FreeRid(hitZone.GetIndexedValue());

                if (entity.TryGetComponent(out BlockEcs blockEcs))
                    PhysicsServer2D.FreeRid(blockEcs.Body);

                if (entity.TryGetComponent(out HurtZoneEcs hurtZone))
                    PhysicsServer2D.FreeRid(hurtZone.Area);
            };

            GlobalClock.Instance.Stopped += () =>
            {
                var commandBuffer = entityStore.GetCommandBuffer();

                entityStore.Query<NoteEcs,Hit,ElementEcs>()
                    .ForEachEntity((ref NoteEcs _,ref Hit _, ref ElementEcs element, Entity entity) =>
                    {
                        commandBuffer.RemoveComponent<Hit>(entity.Id);
                        commandBuffer.RemoveTag<Judged>(entity.Id);
                    });

                commandBuffer.Playback();
            };

        }

        private void refreshSystems()
        {
            Add(new RefreshSystem());
            Add(new PolygonRefresh());
            Add(new ParticlesRefresh());
            Add(new BlockRefresh());
            Add(new TriangleArrayRefresh());
            Add(new RefreshFinalizer());
        }

        private readonly struct EachInitialiser(CommandBuffer buffer) : IEachEntity<ElementEcs>
        {
            public void Execute(ref ElementEcs c1, int id)
            {
                buffer.AddTag<UnInitialized>(id);
            }
        }
    }
}
