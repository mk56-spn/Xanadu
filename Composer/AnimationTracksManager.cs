// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.Composer.TrackVisualiser;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.Tools;

namespace XanaduProject.Composer
{
    public partial class AnimationTracksManager : VBoxContainer
    {
        private readonly EntityStore entityStore;
        private Button button = new(){ CustomMinimumSize = new Vector2(40,40)};

        private ScrollContainer scrollContainer = new(){ CustomMinimumSize = new Vector2(0, 300)};
        private TrackContainer trackContainer;

        public AnimationTracksManager(EntityStore entityStore, TrackHandler trackHandler)
        {
            trackContainer = new TrackContainer(trackHandler);
            this.entityStore = entityStore;

            CustomMinimumSize = new Vector2(0, 400);
            AddChild(button);
            AddChild(scrollContainer);
            scrollContainer.AddChild(trackContainer);

            entityStore.Query<ColorTrack>().ForEachEntity((ref ColorTrack _, Entity e) =>
                trackContainer.AddChild(new ColorTrackVisualizer { Entity = e }));

            trackContainer.CustomMinimumSize = new Vector2((float)trackHandler.TrackLength * 200, 0);

            MouseFilter = MouseFilterEnum.Stop;
        }

      public override void _EnterTree()
        {
            base._EnterTree();

            button.Pressed += () =>
            {
                Entity entity = entityStore.CreateEntity(new ColorTrack
                {
                    KeyFrames = [
                        new ColorKeyFrame(0, Colors.White),
                        new ColorKeyFrame(2, new Color(GD.Randf(), GD.Randf(), GD.Randf())),
                        new ColorKeyFrame(4,  new Color(GD.Randf(), GD.Randf(), GD.Randf()))
                    ]});

                entityStore.Query<ElementEcs>().ForEachEntity((ref ElementEcs _, Entity entity1) =>
                    entity1.AddComponent(new ColorRelation { Target = entity }));

                trackContainer.AddChild(new ColorTrackVisualizer
                {
                    Entity = entity
                });
            };
        }

        private partial class TrackContainer(TrackHandler trackHandler) : VBoxContainer
        {
            public override void _Process(double delta)
            {
                base._Process(delta);
                QueueRedraw();
            }

            public override void _Draw()
            {
                DrawSetTransform(new Vector2((float)(20 + trackHandler.TrackPosition * 200),0));
                DrawLine(Vector2.Zero, Vector2.Down * 200, XanaduColors.XanaduYellow);
            }
        }
    }
}
