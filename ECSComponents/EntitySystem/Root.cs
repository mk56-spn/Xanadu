// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.CharacterSystems;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems;
using XanaduProject.ECSComponents.EntitySystem.InitialiserSystems;
using XanaduProject.ECSComponents.EntitySystem.NoteSystems;
using XanaduProject.ECSComponents.EntitySystem.Refresh_systems;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class Root : SystemRoot
    {
        private readonly EntityStore entityStore;

        public Root(EntityStore entityStore)
        {
            this.entityStore = entityStore;

            queueEntityInitialisation();

            deletionHandling();

            AddStore(entityStore);

            DiProvider.Get<IClock>().Stopped += resetHandling;
            visualsMaster.GameplayerLayer.TreeEntered += setupSystems;
        }

        private IVisualsMaster visualsMaster = DiProvider.Get<IVisualsMaster>();

        #region Entity Managment

        private void queueEntityInitialisation()
        {
            var buffer = entityStore.GetCommandBuffer();

            entityStore.Query<ElementEcs>().EachEntity(new EachInitialiser(buffer));

            buffer.Playback();

        }

        private readonly struct EachInitialiser(CommandBuffer buffer) : IEachEntity<ElementEcs> {
            public void Execute(ref ElementEcs c1, int id)=>
                buffer.AddTag<UnInitialized>(id);
        }

        #endregion


        #region System setup

        private void setupSystems()
        {
            creationSystems();
            refreshSystems();
            noteSystems();
            characterSystems();

            Add(new ColourInterpolatorSystem(entityStore));
            Add(new DebugSystem(entityStore, this));

        }

        private void characterSystems()
        {
            Add(new GroundEffectsSystems());
            Add(new HitEffectSystem());
            Add(new HoldEffectSystem());
        }

        private void creationSystems()
        {
            Add(new CanvasCreatorSystem());
            Add(new NoteCreatorSystem());
            Add(new HurtZoneCreator());
            Add(new DirectionNoteCreator());
            Add(new BlockBodySystem());
            Add(new InitializerSystem());
            Add(new CreatorFinalizer());
        }

        private void refreshSystems()
        {
            Add(new MainRefreshSystem());
            Add(new PolygonRefresh());
            Add(new ParticlesRefresh());
            Add(new BlockRefresh());
            Add(new NoteRefresh());
            Add(new TriangleArrayRefresh());
            Add(new RefreshFinalizer());
        }

        private void noteSystems()
        {
            Add(new NotelineSystem());
            Add(new NoteInputSystem());
            Add(new NoteResultSystem());
            Add(new NoteJudgementTextSystem());
            Add(new NoteBarSystem());
        }

        #endregion

        private void resetHandling()
        {
            var commandBuffer = entityStore.GetCommandBuffer();

            entityStore.Query<NoteEcs, Hit, ElementEcs>()
                .ForEachEntity((ref NoteEcs _, ref Hit _, ref ElementEcs _, Entity entity) =>
                {
                    commandBuffer.RemoveComponent<Hit>(entity.Id);
                    commandBuffer.RemoveComponent<Judged>(entity.Id);
                });

            commandBuffer.Playback();
        }
        private void deletionHandling()
        {
           entityStore.OnEntityDelete += delete =>
            {
                var entity = delete.Entity;
                RenderingServer.FreeRid(entity.GetComponent<ElementEcs>().Canvas);
                if (entity.TryGetComponent(out NoteEcs note))
                {
                    RenderingServer.FreeRid(note.NoteCanvas);
                }
                PhysicsServer2D.FreeRid(entity.GetComponent<SelectionEcs>().Area);


                if (entity.TryGetComponent(out HitZoneEcs hitZone))
                    PhysicsServer2D.FreeRid(hitZone.GetIndexedValue());

                if (entity.TryGetComponent(out BlockEcs blockEcs))
                    PhysicsServer2D.FreeRid(blockEcs.Body);

                if (entity.TryGetComponent(out HurtZoneEcs hurtZone))
                    PhysicsServer2D.FreeRid(hurtZone.Area);
            };
        }
    }
}
