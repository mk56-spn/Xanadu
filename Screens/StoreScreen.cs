// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public abstract partial class StoreScreen : MainScreen
    {
        protected readonly SystemRoot Root = new();
        protected readonly EntityStore Store = new()
        {
            JobRunner = new ParallelJobRunner(10, "n"),
        };

        protected StoreScreen()
        {
            DiProvider.Register(c => { c.AddSingleton(Store); });
        }
        protected abstract void PostBaseSystems(SystemRoot root);

        public override void _EnterTree()
        {
            base._EnterTree();
            PostBaseSystems(Root);
            Root.AddStore(Store);
        }

        public override void _Process(double delta)
        {
            Root.Update(new UpdateTick((float)delta,Time.GetTicksMsec()));
        }
    }
}
