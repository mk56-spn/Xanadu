// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.GameDependencies;
using XanaduProject.Singleton;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Systems
{
    public class PoseAnimationUiBuilderSystem : QuerySystem
    {
        private readonly EntityStore store = DiProvider.Get<EntityStore>();

        public PoseAnimationUiBuilderSystem(PoseAnimatingLayout target)
        {
            var v = PoseAnimatingScreen.Info;
            float spacing = 1500 / v.Duration;
            Logger.AddLog(LogCategory.General, "PoseAnimationUiBuilderSystem");

            VBoxContainer c = new VBoxContainer(){ SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            target.AddChild(c);

            store.Query<AngleArrayEcs,FloatArrayEcs, NameEcs>().ForEachEntity(((ref AngleArrayEcs component1, ref FloatArrayEcs component2, ref NameEcs component3, Entity entity) =>
            {

                Logger.AddLog(LogCategory.General, "AngleArrayEcs");
                HBoxContainer cont = new HBoxContainer();
                target.AddChild(cont);
                cont.AddChild(new Label(){ Text = component3.Name, CustomMinimumSize = new Vector2(170, 0)});
                cont.AddChild(new FloatTrackVisualizer(c)
                {
                    Spacing = spacing,
                    Entity = entity
                });
            }));


            store.Query<VectorArrayEcs,FloatArrayEcs, NameEcs>().ForEachEntity(((ref VectorArrayEcs component1, ref FloatArrayEcs component2, ref NameEcs name, Entity entity) =>
            {
                HBoxContainer cont = new HBoxContainer();
                target.AddChild(cont);
                cont.AddChild(new Label(){ Text = name.Name, CustomMinimumSize = new Vector2(170, 0)});
                cont.AddChild(new VectorTrackVisualizer(c)
                {
                    Spacing = spacing,
                    Entity = entity
                });

            }));



        }

        protected override void OnUpdate(){}
    }
}
