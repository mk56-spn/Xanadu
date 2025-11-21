// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.ColourChannels;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;
using XanaduProject.UiElements;
using XanaduProject.Utils;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class ColourTrackSystem : QuerySystem<ColorArrayEcs>
    {
        public ColourTrackSystem()
        {
            Filter.AllTags(Tags.Get<Dormant>());
        }

        private readonly Container editContainer = new();
        private readonly VBoxContainer container = new();

        protected override void OnAddStore(EntityStore store)
        {
            AnimatedHoverButton button = new("Add Colour Track", 20);

            button.Pressed += () =>
                store.CreateEntity(new FloatArrayEcs() { Points = []} ,
                    new ColorArrayEcs(){ Points = []},
                    new ActiveColourEcs(),
                    new NameEcs(Guid.CreateVersion7().ToString()),
                    Tags.Get<Dormant>());

            IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();
            visuals.AddTabToMain(container.Child(new HBoxContainer()
                .AddChildren(button, editContainer)));

        }

        protected override void OnUpdate()
        {

            Query.ForEachEntity(((ref ColorArrayEcs _, Entity channelEntity) =>
            {
                Button primary = new AnimatedHoverButton("1", 30);
                primary.Pressed += () =>
                {
                    var buf = GameServices.Store.GetCommandBuffer();
                    DiProvider.Get<IComposer>().Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _, Entity entity) =>
                    {
                        if (entity.HasComponent<TargetGroupEcs>())
                        {
                            entity.GetComponent<TargetGroupEcs>().Primary = channelEntity;
                        }
                        else
                        {
                            buf.AddComponent(entity.Id, new TargetGroupEcs(){ Primary =  channelEntity });
                        }
                    });
                    buf.Playback();
                };

                Button secondary = new AnimatedHoverButton("2", 30);
                secondary.Pressed += () =>
                {
                    var buf = GameServices.Store.GetCommandBuffer();
                    DiProvider.Get<IComposer>().Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _, Entity entity) =>
                    {
                        if (entity.HasComponent<TargetGroupEcs>())
                        {
                            entity.GetComponent<TargetGroupEcs>().Secondary = channelEntity;
                        }
                        else
                        {
                            buf.AddComponent(entity.Id, new TargetGroupEcs(){ Secondary = channelEntity });
                        }
                    });
                    buf.Playback();
                };



                container.Child(
                    new HBoxContainer().AddChildren(
                        primary,
                        secondary,
                        new ColorTrackVisualizer(editContainer){ Entity =  channelEntity}));
            } ));

        }
    }
}
