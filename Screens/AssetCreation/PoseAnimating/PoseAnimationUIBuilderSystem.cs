// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Singleton;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public class PoseAnimationUiBuilderSystem : QuerySystem
    {
        private readonly EntityStore store = DiProvider.Get<EntityStore>();

        public PoseAnimationUiBuilderSystem(Container target)
        {

            var v = store.GetUniqueEntity(PoseAnimatingScreen.ANIMATION).GetComponent<AnimationInfo>();
            float spacing = 1500 / v.Duration;
            Logger.AddLog(LogCategory.General, "PoseAnimationUiBuilderSystem");

            Container c = new Container()
            {
                CustomMinimumSize = new Vector2(200,200)
            };
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
