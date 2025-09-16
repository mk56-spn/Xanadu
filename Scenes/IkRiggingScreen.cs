// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.ScreenStructure;
using Logger = XanaduProject.Singleton.Logger;

namespace XanaduProject.Scenes
{
    public partial class IkRiggingScreen : MainScreen
    {
        private readonly EntityStore entityStore = new()
        {
            JobRunner = new ParallelJobRunner(10, "n")
        };
        private readonly SystemRoot simulationRoot;

        public IkRiggingScreen()
        {
            DiProvider.Register(c=>
                c.AddSingleton(entityStore));

            PoseBuilder.BuildPose();

            simulationRoot = new SystemRoot(entityStore)
            {
                new BoneTransformSystem(),
                new IkSolverSystem(),
                new BoneRenderingSystem(),
              /*  new SkinRenderingSystem2D()*/
            };

            entityStore.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
               rootEcs.Canvas.SetTransform(new Transform2D(0, new Vector2(200,200)));
               rootEcs.Canvas.SetParent(GetCanvasItem());
            });

            Logger.AddLog(LogCategory.Animation, "Ik rigging screen initialized");
        }

        public override void _Process(double delta)
        {
            simulationRoot.Update(default);
        }
    }
}
